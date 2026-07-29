namespace KirasaEngine.MGL.Rendering;

/// <summary>
/// Owns every GPU resource derived from <see cref="Scene"/>/<see cref="Model"/> data for one
/// <see cref="IGraphicsDevice"/> (meshes, pipelines, per-material/-batch buffers) and caches them across
/// calls, so repeated <see cref="RenderToTexture"/> calls only pay for uploading data that actually changed.
/// Does not own or dispose the device itself.
///
/// <para><b>Pipeline (all offscreen, one pass writes the next pass's input texture):</b> shadow depth pass
/// (scene from the light's POV, if <see cref="PostProcessSettings.ShadowsActive"/>) -> depth+normal prepass
/// (scene from the camera, if <see cref="PostProcessSettings.SSAOActive"/>) -> SSAO pass (reads the prepass)
/// -> main forward-lit HDR pass (samples the shadow map and AO texture) -> bloom bright-pass+blur (H then V,
/// if <see cref="PostProcessSettings.BloomActive"/>) -> composite (tonemap + bloom + vignette/color grade) ->
/// FXAA (if <see cref="PostProcessSettings.FXAAActive"/>) -> <see cref="IGraphicsDevice.ReadRenderTarget"/> on
/// whichever of the last two passes actually ran. Every optional pass is skipped outright when its
/// <see cref="RenderQuality"/> is <see cref="RenderQuality.Off"/>, so disabling everything reduces to exactly
/// the single forward pass this renderer started as.</para>
/// </summary>
public sealed class SceneRenderer : IDisposable
{
    private readonly IGraphicsDevice _device;
    public PostProcessSettings Settings { get; set; }

    private readonly Dictionary<Mesh, MeshGpuResources> _meshCache = [];
    private readonly Dictionary<(string ShaderName, BlendMode Blend, bool DoubleSided), IPipeline> _pipelineCache = [];
    private readonly Dictionary<Material, IBuffer> _drawConstantsCache = [];
    private readonly Dictionary<object, (IBuffer Buffer, uint Capacity)> _instanceBufferCache = [];

    /// <summary>Immutable, no per-material variation — one cached pipeline per full-screen/geometry-only pass.</summary>
    private readonly Dictionary<string, IPipeline> _passPipelineCache = [];

    /// <summary>Keyed by a tag unique to each logical target (e.g. "Shadow", "Hdr") — recreated only when requested dimensions/format change.</summary>
    private readonly Dictionary<string, IRenderTarget> _targetCache = [];

    private IResourceLayout? _resourceLayout;
    private IBuffer? _frameConstantsBuffer;
    private ITexture? _whiteTexture;

    /// <summary>1x1 R32Float texture holding 1.0, bound in place of the shadow map / AO texture when either is disabled (the shader ignores it via the enable flags in <see cref="ShaderResourceLayouts.FrameConstantsData"/>, but every backend still needs a format-compatible resource bound).</summary>
    private ITexture? _placeholderR32Texture;
    private ISampler? _defaultSampler;
    private ISampler? _clampSampler;

    private IResourceLayout? _shadowResourceLayout;
    private IBuffer? _shadowConstantsBuffer;
    private IResourceLayout? _prepassResourceLayout;
    private IBuffer? _prepassConstantsBuffer;
    private IResourceLayout? _ssaoResourceLayout;
    private IBuffer? _ssaoConstantsBuffer;
    private IResourceLayout? _blurResourceLayout;
    private IBuffer? _blurConstantsBuffer;
    private IResourceLayout? _compositeResourceLayout;
    private IBuffer? _compositeConstantsBuffer;
    private IResourceLayout? _fxaaResourceLayout;
    private IBuffer? _fxaaConstantsBuffer;

    private IBuffer? _fullscreenVertexBuffer;
    private IBuffer? _fullscreenIndexBuffer;

    private readonly record struct MeshGpuResources(IBuffer VertexBuffer, IBuffer IndexBuffer, int Version);

    public SceneRenderer(IGraphicsDevice device, PostProcessSettings? settings = null)
    {
        _device = device;
        Settings = settings ?? PostProcessSettings.Default;
    }

    /// <summary>Renders the scene once into an offscreen target and reads it back as top-left-origin RGBA8 bytes.</summary>
    public byte[] RenderToTexture(Scene scene, uint width, uint height, SceneNode? cameraNode = null)
    {
        cameraNode ??= scene.FindCameraNode() ?? throw new InvalidOperationException("Scene contains no camera node.");
        var camera = cameraNode.Camera!;

        EnsureCommonResources();
        var groups = BuildDrawGroups(scene);
        var frameDisposables = new List<IDisposable>();

        var lightNode = scene.FindLightNodes().FirstOrDefault();
        var lightViewProjection = Matrix4x4.Identity;
        var shadowsActive = Settings.ShadowsActive && lightNode is not null;
        ITexture? shadowMapTexture = null;

        if (shadowsActive)
        {
            lightViewProjection = ComputeLightViewProjection(scene, lightNode!, Settings);
            var shadowTarget = GetOrCreateTarget("Shadow", Settings.ShadowMapResolution, Settings.ShadowMapResolution, TextureFormat.R32Float, TextureFormat.Depth24Stencil8);
            RenderShadowPass(groups, shadowTarget, lightViewProjection, frameDisposables);
            shadowMapTexture = shadowTarget.ColorTexture;
        }

        var aoActive = Settings.SSAOActive;
        ITexture? aoTexture = null;
        if (aoActive)
        {
            var prepassTarget = GetOrCreateTarget("Prepass", width, height, TextureFormat.Rgba16Float, TextureFormat.Depth24Stencil8);
            RenderPrepass(groups, prepassTarget, camera, cameraNode.Transform, width, height, frameDisposables);

            var aoTarget = GetOrCreateTarget("AO", width, height, TextureFormat.R32Float, null);
            RenderSsaoPass(prepassTarget.ColorTexture, aoTarget, camera, frameDisposables);
            aoTexture = aoTarget.ColorTexture;
        }

        var hdrTarget = GetOrCreateTarget("Hdr", width, height, TextureFormat.Rgba16Float, TextureFormat.Depth24Stencil8);
        RenderMainPass(groups, scene, camera, cameraNode.Transform, hdrTarget, width, height, lightViewProjection, shadowsActive, shadowMapTexture, aoActive, aoTexture, frameDisposables);

        ITexture? bloomTexture = null;
        if (Settings.BloomActive)
        {
            var bloomA = GetOrCreateTarget("BloomA", width, height, TextureFormat.Rgba16Float, null);
            var bloomB = GetOrCreateTarget("BloomB", width, height, TextureFormat.Rgba16Float, null);
            RenderBlurPass(hdrTarget.ColorTexture, bloomA, width, height, horizontal: true, applyThreshold: true, frameDisposables);
            RenderBlurPass(bloomA.ColorTexture, bloomB, width, height, horizontal: false, applyThreshold: false, frameDisposables);
            bloomTexture = bloomB.ColorTexture;
        }

        var compositeTarget = GetOrCreateTarget("Composite", width, height, TextureFormat.Rgba8UNorm, null);
        RenderCompositePass(hdrTarget.ColorTexture, bloomTexture, compositeTarget, frameDisposables);

        var finalTarget = compositeTarget;
        if (Settings.FXAAActive)
        {
            var fxaaTarget = GetOrCreateTarget("Fxaa", width, height, TextureFormat.Rgba8UNorm, null);
            RenderFxaaPass(compositeTarget.ColorTexture, fxaaTarget, width, height, frameDisposables);
            finalTarget = fxaaTarget;
        }

        foreach (var disposable in frameDisposables) disposable.Dispose();

        return _device.ReadRenderTarget(finalTarget);
    }

    // ---------------------------------------------------------------------
    // Pass 1: shadow depth (scene geometry, light's point of view)
    // ---------------------------------------------------------------------
    private void RenderShadowPass(Dictionary<(Mesh Mesh, Material Material), List<InstanceData>> groups, IRenderTarget target, Matrix4x4 lightViewProjection, List<IDisposable> frameDisposables)
    {
        var pipeline = GetOrCreatePassPipeline("Shadow", () => _device.Factory.CreatePipeline(new PipelineDescription
        {
            ShaderSet = _device.Factory.CreateShaderSet(new ShaderSetDescription
            {
                ShaderName = "ShadowDepth",
                VertexLayouts = [VertexPNCT.GetVertexLayout(), InstanceData.GetVertexLayout(4)],
            }),
            ResourceLayout = _shadowResourceLayout!,
            CullMode = CullMode.None,
            ColorFormat = TextureFormat.R32Float,
            DepthFormat = TextureFormat.Depth24Stencil8,
        }));

        var constants = new ShaderResourceLayouts.ShadowConstantsData { LightViewProjection = lightViewProjection };

        var cmd = _device.CreateCommandList();
        cmd.Begin();
        cmd.SetRenderTarget(target);
        cmd.SetViewport(new Viewport(0, 0, target.Width, target.Height));
        cmd.ClearColor(new Vector4(1, 1, 1, 1));
        cmd.ClearDepthStencil();
        cmd.UpdateBuffer(_shadowConstantsBuffer!, AsBytes(ref constants));

        var resourceSet = _device.Factory.CreateResourceSet(new ResourceSetDescription { Layout = _shadowResourceLayout!, Resources = [_shadowConstantsBuffer!] });
        frameDisposables.Add(resourceSet);

        cmd.SetPipeline(pipeline);
        cmd.SetResourceSet(0, resourceSet);
        foreach (var (key, instances) in groups)
            DrawGeometryOnly(cmd, key.Mesh, instances, key, frameDisposables);

        cmd.End();
        _device.Submit(cmd);
        cmd.Dispose();
    }

    // ---------------------------------------------------------------------
    // Pass 2: depth+normal prepass (scene geometry, camera's point of view) — feeds SSAO
    // ---------------------------------------------------------------------
    private void RenderPrepass(Dictionary<(Mesh Mesh, Material Material), List<InstanceData>> groups, IRenderTarget target, Camera camera, Transform cameraTransform, uint width, uint height, List<IDisposable> frameDisposables)
    {
        var pipeline = GetOrCreatePassPipeline("Prepass", () => _device.Factory.CreatePipeline(new PipelineDescription
        {
            ShaderSet = _device.Factory.CreateShaderSet(new ShaderSetDescription
            {
                ShaderName = "DepthNormalPrepass",
                VertexLayouts = [VertexPNCT.GetVertexLayout(), InstanceData.GetVertexLayout(4)],
            }),
            ResourceLayout = _prepassResourceLayout!,
            CullMode = CullMode.Back,
            ColorFormat = TextureFormat.Rgba16Float,
            DepthFormat = TextureFormat.Depth24Stencil8,
        }));

        var view = camera.GetViewMatrix(cameraTransform);
        var projection = camera.GetProjectionMatrix(width / (float)height);
        var constants = new ShaderResourceLayouts.PrepassConstantsData { ViewProjection = view * projection, View = view };

        var cmd = _device.CreateCommandList();
        cmd.Begin();
        cmd.SetRenderTarget(target);
        cmd.SetViewport(new Viewport(0, 0, width, height));
        cmd.ClearColor(new Vector4(0, 0, 0, 1000f));
        cmd.ClearDepthStencil();
        cmd.UpdateBuffer(_prepassConstantsBuffer!, AsBytes(ref constants));

        var resourceSet = _device.Factory.CreateResourceSet(new ResourceSetDescription { Layout = _prepassResourceLayout!, Resources = [_prepassConstantsBuffer!] });
        frameDisposables.Add(resourceSet);

        cmd.SetPipeline(pipeline);
        cmd.SetResourceSet(0, resourceSet);
        foreach (var (key, instances) in groups)
            DrawGeometryOnly(cmd, key.Mesh, instances, key, frameDisposables);

        cmd.End();
        _device.Submit(cmd);
        cmd.Dispose();
    }

    /// <summary>Shared by the shadow and prepass passes: both draw every mesh+instance group with only position/normal/instance-transform bound, no material data.</summary>
    private void DrawGeometryOnly(ICommandList cmd, Mesh mesh, IReadOnlyList<InstanceData> instances, object instanceCacheKey, List<IDisposable> frameDisposables)
    {
        var meshRes = GetOrUploadMesh(mesh);
        var instanceBuffer = GetOrUploadInstanceBuffer(cmd, instanceCacheKey, instances);
        _ = frameDisposables;

        cmd.SetVertexBuffer(0, meshRes.VertexBuffer);
        cmd.SetVertexBuffer(1, instanceBuffer);
        cmd.SetIndexBuffer(meshRes.IndexBuffer, IndexFormat.UInt32);
        cmd.DrawIndexed((uint)mesh.Indices.Length, (uint)instances.Count);
    }

    // ---------------------------------------------------------------------
    // Pass 3: SSAO (full-screen, reads the prepass output)
    // ---------------------------------------------------------------------
    private void RenderSsaoPass(ITexture normalDepthTexture, IRenderTarget target, Camera camera, List<IDisposable> frameDisposables)
    {
        var pipeline = GetOrCreatePassPipeline("SSAO", () => CreateFullscreenPipeline("SSAO", _ssaoResourceLayout!, TextureFormat.R32Float));

        var tanHalfFov = MathF.Tan(camera.FieldOfViewRadians * 0.5f);
        var aspect = target.Width / (float)target.Height;
        var constants = new ShaderResourceLayouts.SSAOConstantsData
        {
            Params0 = new Vector4(tanHalfFov, aspect, Settings.SSAORadius, Settings.SSAOPower),
            Params1 = new Vector4(Settings.SSAOSampleCount, 0.02f, 0, 0),
        };

        var cmd = _device.CreateCommandList();
        cmd.Begin();
        cmd.SetRenderTarget(target);
        cmd.SetViewport(new Viewport(0, 0, target.Width, target.Height));
        cmd.UpdateBuffer(_ssaoConstantsBuffer!, AsBytes(ref constants));

        var resourceSet = _device.Factory.CreateResourceSet(new ResourceSetDescription
        {
            Layout = _ssaoResourceLayout!,
            Resources = [_ssaoConstantsBuffer!, normalDepthTexture, _clampSampler!],
        });
        frameDisposables.Add(resourceSet);

        DrawFullscreenTriangle(cmd, pipeline, resourceSet);
        cmd.End();
        _device.Submit(cmd);
        cmd.Dispose();
    }

    // ---------------------------------------------------------------------
    // Pass 4: main forward-lit HDR pass (existing draw-batch loop, extended with shadow/AO sampling)
    // ---------------------------------------------------------------------
    private void RenderMainPass(
        Dictionary<(Mesh Mesh, Material Material), List<InstanceData>> groups, Scene scene, Camera camera, Transform cameraTransform,
        IRenderTarget target, uint width, uint height, Matrix4x4 lightViewProjection, bool shadowsActive, ITexture? shadowMapTexture,
        bool aoActive, ITexture? aoTexture, List<IDisposable> frameDisposables)
    {
        var cmd = _device.CreateCommandList();
        cmd.Begin();
        cmd.SetRenderTarget(target);
        cmd.SetViewport(new Viewport(0, 0, width, height));
        cmd.ClearColor(scene.BackgroundColor);
        cmd.ClearDepthStencil();

        UpdateFrameConstants(cmd, scene, camera, cameraTransform, width, height, lightViewProjection, shadowsActive, aoActive);

        var shadowMap = shadowMapTexture ?? _placeholderR32Texture!;
        var ao = aoTexture ?? _placeholderR32Texture!;

        foreach (var (key, instances) in groups)
            DrawBatch(cmd, key.Mesh, key.Material, instances, key, shadowMap, ao, frameDisposables);

        foreach (var batch in scene.InstancedBatches)
        {
            if (batch.Instances.Count == 0) continue;
            DrawBatch(cmd, batch.Mesh, batch.Material, batch.Instances, batch, shadowMap, ao, frameDisposables);
            batch.ClearDirty();
        }

        cmd.End();
        _device.Submit(cmd);
        cmd.Dispose();
    }

    private void DrawBatch(ICommandList cmd, Mesh mesh, Material material, IReadOnlyList<InstanceData> instances, object instanceCacheKey, ITexture shadowMap, ITexture ao, List<IDisposable> frameDisposables)
    {
        var meshRes = GetOrUploadMesh(mesh);
        var pipeline = GetOrCreatePipeline(material);
        var drawConstants = GetOrCreateDrawConstantsBuffer(material);

        var drawData = new ShaderResourceLayouts.DrawConstantsData { BaseColor = material.BaseColor };
        cmd.UpdateBuffer(drawConstants, AsBytes(ref drawData));

        var instanceBuffer = GetOrUploadInstanceBuffer(cmd, instanceCacheKey, instances);

        var resourceSet = _device.Factory.CreateResourceSet(new ResourceSetDescription
        {
            Layout = _resourceLayout!,
            Resources =
            [
                _frameConstantsBuffer!, drawConstants, material.BaseColorTexture ?? _whiteTexture!, _defaultSampler!,
                shadowMap, _clampSampler!, ao, _clampSampler!,
            ],
        });
        // Descriptor allocation for D3D12/Vulkan is not free; disposal is deferred to after Submit() because
        // those backends only truly execute the command list at that point (see the synchronous-submit note
        // on IGraphicsDevice.Submit) — freeing it earlier would invalidate a binding the GPU hasn't read yet.
        frameDisposables.Add(resourceSet);

        cmd.SetPipeline(pipeline);
        cmd.SetVertexBuffer(0, meshRes.VertexBuffer);
        cmd.SetVertexBuffer(1, instanceBuffer);
        cmd.SetIndexBuffer(meshRes.IndexBuffer, IndexFormat.UInt32);
        cmd.SetResourceSet(0, resourceSet);
        cmd.DrawIndexed((uint)mesh.Indices.Length, (uint)instances.Count);
    }

    private void UpdateFrameConstants(ICommandList cmd, Scene scene, Camera camera, Transform cameraTransform, uint width, uint height, Matrix4x4 lightViewProjection, bool shadowsActive, bool aoActive)
    {
        var view = camera.GetViewMatrix(cameraTransform);
        var projection = camera.GetProjectionMatrix(width / (float)height);
        // Uploaded byte-for-byte, no transpose: System.Numerics stores matrices row-major, and GLSL/HLSL
        // read a flat float16 buffer as column-major, so reinterpreting the same bytes performs the
        // row-vector -> column-vector convention switch for free. Adding an explicit transpose here would
        // cancel that out and corrupt the result (this was a real bug caught by the OpenGL smoke test).
        var viewProjection = view * projection;

        var lightNode = scene.FindLightNodes().FirstOrDefault();
        var lightDirection = new Vector4(0, -1, 0, 0);
        var lightColor = new Vector4(1, 1, 1, 1);
        if (lightNode is not null)
        {
            lightDirection = new Vector4(lightNode.Transform.Forward, 0);
            lightColor = new Vector4(lightNode.Light!.Color, lightNode.Light.Intensity);
        }

        var shadowTexelSize = Settings.ShadowMapResolution > 0 ? 1f / Settings.ShadowMapResolution : 0f;
        var data = new ShaderResourceLayouts.FrameConstantsData
        {
            ViewProjection = viewProjection,
            LightViewProjection = lightViewProjection,
            LightDirection = lightDirection,
            LightColor = lightColor,
            AmbientColor = new Vector4(scene.AmbientColor, 1f),
            ShadowParams = new Vector4(shadowTexelSize, Settings.ShadowBias, Settings.ShadowPcfRadius, shadowsActive ? 1f : 0f),
            ScreenParams = new Vector4(width, height, aoActive ? 1f : 0f, 0f),
        };
        cmd.UpdateBuffer(_frameConstantsBuffer!, AsBytes(ref data));
    }

    // ---------------------------------------------------------------------
    // Pass 5/6: separable bloom blur (reused for bright-pass+horizontal, then plain vertical)
    // ---------------------------------------------------------------------
    private void RenderBlurPass(ITexture source, IRenderTarget target, uint width, uint height, bool horizontal, bool applyThreshold, List<IDisposable> frameDisposables)
    {
        var pipeline = GetOrCreatePassPipeline("Blur", () => CreateFullscreenPipeline("Blur", _blurResourceLayout!, TextureFormat.Rgba16Float));

        var constants = new ShaderResourceLayouts.BlurConstantsData
        {
            Params0 = new Vector4(1f / width, 1f / height, horizontal ? 0f : 1f, Settings.BloomBlurRadius),
            Params1 = new Vector4(Settings.BloomThreshold, applyThreshold ? 1f : 0f, 0, 0),
        };

        var cmd = _device.CreateCommandList();
        cmd.Begin();
        cmd.SetRenderTarget(target);
        cmd.SetViewport(new Viewport(0, 0, width, height));
        cmd.UpdateBuffer(_blurConstantsBuffer!, AsBytes(ref constants));

        var resourceSet = _device.Factory.CreateResourceSet(new ResourceSetDescription { Layout = _blurResourceLayout!, Resources = [_blurConstantsBuffer!, source, _clampSampler!] });
        frameDisposables.Add(resourceSet);

        DrawFullscreenTriangle(cmd, pipeline, resourceSet);
        cmd.End();
        _device.Submit(cmd);
        cmd.Dispose();
    }

    // ---------------------------------------------------------------------
    // Pass 7: composite (tonemap + bloom + vignette/color grade)
    // ---------------------------------------------------------------------
    private void RenderCompositePass(ITexture hdrColor, ITexture? bloom, IRenderTarget target, List<IDisposable> frameDisposables)
    {
        var pipeline = GetOrCreatePassPipeline("Composite", () => CreateFullscreenPipeline("Composite", _compositeResourceLayout!, TextureFormat.Rgba8UNorm));

        var constants = new ShaderResourceLayouts.CompositeConstantsData
        {
            Params0 = new Vector4(Settings.BloomIntensity, Settings.VignetteIntensity, Settings.Saturation, Settings.Contrast),
            Params1 = new Vector4(bloom is not null ? 1f : 0f, Settings.VignetteActive ? 1f : 0f, 0, 0),
        };

        var cmd = _device.CreateCommandList();
        cmd.Begin();
        cmd.SetRenderTarget(target);
        cmd.SetViewport(new Viewport(0, 0, target.Width, target.Height));
        cmd.UpdateBuffer(_compositeConstantsBuffer!, AsBytes(ref constants));

        var resourceSet = _device.Factory.CreateResourceSet(new ResourceSetDescription
        {
            Layout = _compositeResourceLayout!,
            Resources = [_compositeConstantsBuffer!, hdrColor, _clampSampler!, bloom ?? hdrColor, _clampSampler!],
        });
        frameDisposables.Add(resourceSet);

        DrawFullscreenTriangle(cmd, pipeline, resourceSet);
        cmd.End();
        _device.Submit(cmd);
        cmd.Dispose();
    }

    // ---------------------------------------------------------------------
    // Pass 8: FXAA (final antialiasing, reads the composite pass's LDR output)
    // ---------------------------------------------------------------------
    private void RenderFxaaPass(ITexture source, IRenderTarget target, uint width, uint height, List<IDisposable> frameDisposables)
    {
        var pipeline = GetOrCreatePassPipeline("FXAA", () => CreateFullscreenPipeline("FXAA", _fxaaResourceLayout!, TextureFormat.Rgba8UNorm));

        var constants = new ShaderResourceLayouts.FXAAConstantsData { Params0 = new Vector4(1f / width, 1f / height, 0, 0) };

        var cmd = _device.CreateCommandList();
        cmd.Begin();
        cmd.SetRenderTarget(target);
        cmd.SetViewport(new Viewport(0, 0, width, height));
        cmd.UpdateBuffer(_fxaaConstantsBuffer!, AsBytes(ref constants));

        var resourceSet = _device.Factory.CreateResourceSet(new ResourceSetDescription { Layout = _fxaaResourceLayout!, Resources = [_fxaaConstantsBuffer!, source, _clampSampler!] });
        frameDisposables.Add(resourceSet);

        DrawFullscreenTriangle(cmd, pipeline, resourceSet);
        cmd.End();
        _device.Submit(cmd);
        cmd.Dispose();
    }

    // ---------------------------------------------------------------------
    // Shared helpers
    // ---------------------------------------------------------------------
    private IPipeline CreateFullscreenPipeline(string shaderName, IResourceLayout layout, TextureFormat colorFormat) =>
        _device.Factory.CreatePipeline(new PipelineDescription
        {
            ShaderSet = _device.Factory.CreateShaderSet(new ShaderSetDescription { ShaderName = shaderName, VertexLayouts = [PostProcessVertex.GetVertexLayout()] }),
            ResourceLayout = layout,
            CullMode = CullMode.None,
            DepthTestEnabled = false,
            DepthWriteEnabled = false,
            DepthFormat = null,
            ColorFormat = colorFormat,
        });

    private void DrawFullscreenTriangle(ICommandList cmd, IPipeline pipeline, IResourceSet resourceSet)
    {
        cmd.SetPipeline(pipeline);
        cmd.SetVertexBuffer(0, _fullscreenVertexBuffer!);
        cmd.SetIndexBuffer(_fullscreenIndexBuffer!, IndexFormat.UInt32);
        cmd.SetResourceSet(0, resourceSet);
        cmd.DrawIndexed(3);
    }

    private IPipeline GetOrCreatePassPipeline(string key, Func<IPipeline> create)
    {
        if (_passPipelineCache.TryGetValue(key, out var existing)) return existing;
        var pipeline = create();
        _passPipelineCache[key] = pipeline;
        return pipeline;
    }

    private IRenderTarget GetOrCreateTarget(string key, uint width, uint height, TextureFormat colorFormat, TextureFormat? depthFormat)
    {
        if (_targetCache.TryGetValue(key, out var existing) && existing.Width == width && existing.Height == height && existing.ColorFormat == colorFormat)
            return existing;

        existing?.Dispose();
        var target = _device.Factory.CreateRenderTarget(new RenderTargetDescription(width, height, colorFormat, depthFormat));
        _targetCache[key] = target;
        return target;
    }

    /// <summary>
    /// Builds a light-facing orthographic frustum tightly enclosing the scene: the union of every
    /// renderable node's/instance's world position, padded by <see cref="PostProcessSettings.ShadowFrustumPadding"/>
    /// to account for individual mesh extents (this project has no per-mesh bounding volume yet, so treating
    /// instances as points and padding by a fixed margin is a deliberate simplification over true bounds).
    /// </summary>
    private static Matrix4x4 ComputeLightViewProjection(Scene scene, SceneNode lightNode, PostProcessSettings settings)
    {
        var lightDirection = Vector3.Normalize(lightNode.Transform.Forward);

        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        var any = false;

        foreach (var node in scene.Traverse())
        {
            if (node.Renderer is null) continue;
            var p = node.Transform.WorldPosition;
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
            any = true;
        }

        foreach (var batch in scene.InstancedBatches)
        foreach (var instance in batch.Instances)
        {
            var p = instance.World.Translation;
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
            any = true;
        }

        if (!any)
        {
            min = new Vector3(-5);
            max = new Vector3(5);
        }

        var padding = new Vector3(settings.ShadowFrustumPadding);
        min -= padding;
        max += padding;

        var center = (min + max) * 0.5f;
        var radius = MathF.Max(Vector3.Distance(min, max) * 0.5f, 1f);

        var up = MathF.Abs(Vector3.Dot(lightDirection, Vector3.UnitY)) > 0.999f ? Vector3.UnitZ : Vector3.UnitY;
        var eye = center - lightDirection * radius * 2f;
        var view = Matrix4x4.CreateLookAt(eye, center, up);
        var projection = Matrix4x4.CreateOrthographic(radius * 2f, radius * 2f, 0.01f, radius * 4f);
        return view * projection;
    }

    private static Dictionary<(Mesh Mesh, Material Material), List<InstanceData>> BuildDrawGroups(Scene scene)
    {
        var groups = new Dictionary<(Mesh Mesh, Material Material), List<InstanceData>>();
        foreach (var node in scene.Traverse())
        {
            if (node.Renderer is not { } renderer) continue;
            var key = (renderer.Mesh, renderer.Material);
            if (!groups.TryGetValue(key, out var list)) groups[key] = list = [];
            list.Add(new InstanceData(node.Transform.WorldMatrix, Vector4.One));
        }
        return groups;
    }

    private MeshGpuResources GetOrUploadMesh(Mesh mesh)
    {
        if (_meshCache.TryGetValue(mesh, out var cached) && cached.Version == mesh.Version)
            return cached;

        if (cached.VertexBuffer is not null)
        {
            cached.VertexBuffer.Dispose();
            cached.IndexBuffer.Dispose();
        }

        var vertexBytes = MemoryMarshal.AsBytes(mesh.Vertices.AsSpan());
        var vertexBuffer = _device.Factory.CreateBuffer(new BufferDescription((uint)vertexBytes.Length, BufferUsage.Vertex), vertexBytes);

        var indexBytes = MemoryMarshal.AsBytes(mesh.Indices.AsSpan());
        var indexBuffer = _device.Factory.CreateBuffer(new BufferDescription((uint)indexBytes.Length, BufferUsage.Index), indexBytes);

        var resources = new MeshGpuResources(vertexBuffer, indexBuffer, mesh.Version);
        _meshCache[mesh] = resources;
        return resources;
    }

    private IPipeline GetOrCreatePipeline(Material material)
    {
        var key = (material.ShaderName, material.Blend, material.DoubleSided);
        if (_pipelineCache.TryGetValue(key, out var pipeline)) return pipeline;

        var shaderSet = _device.Factory.CreateShaderSet(new ShaderSetDescription
        {
            ShaderName = material.ShaderName,
            VertexLayouts = [VertexPNCT.GetVertexLayout(), InstanceData.GetVertexLayout(4)],
        });

        pipeline = _device.Factory.CreatePipeline(new PipelineDescription
        {
            ShaderSet = shaderSet,
            ResourceLayout = _resourceLayout!,
            CullMode = material.DoubleSided ? CullMode.None : CullMode.Back,
            Blend = material.Blend,
            ColorFormat = TextureFormat.Rgba16Float,
            DepthFormat = TextureFormat.Depth24Stencil8,
        });

        _pipelineCache[key] = pipeline;
        return pipeline;
    }

    private IBuffer GetOrCreateDrawConstantsBuffer(Material material)
    {
        if (_drawConstantsCache.TryGetValue(material, out var buffer)) return buffer;
        buffer = _device.Factory.CreateBuffer(new BufferDescription(ShaderResourceLayouts.DrawConstantsData.SizeInBytes, BufferUsage.Uniform | BufferUsage.Dynamic));
        _drawConstantsCache[material] = buffer;
        return buffer;
    }

    private IBuffer GetOrUploadInstanceBuffer(ICommandList cmd, object key, IReadOnlyList<InstanceData> instances)
    {
        var requiredBytes = (uint)instances.Count * InstanceData.SizeInBytes;

        if (!_instanceBufferCache.TryGetValue(key, out var entry) || entry.Capacity < requiredBytes)
        {
            entry.Buffer?.Dispose();
            var buffer = _device.Factory.CreateBuffer(new BufferDescription(requiredBytes, BufferUsage.Vertex | BufferUsage.Dynamic));
            entry = (buffer, requiredBytes);
            _instanceBufferCache[key] = entry;
        }

        // No transpose needed here either — see the comment in UpdateFrameConstants.
        var gpuData = new InstanceData[instances.Count];
        for (var i = 0; i < instances.Count; i++)
            gpuData[i] = instances[i];

        cmd.UpdateBuffer(entry.Buffer, MemoryMarshal.AsBytes(gpuData.AsSpan()));
        return entry.Buffer;
    }

    private void EnsureCommonResources()
    {
        if (_resourceLayout is not null) return;

        _resourceLayout = _device.Factory.CreateResourceLayout(ShaderResourceLayouts.Standard);
        _frameConstantsBuffer = _device.Factory.CreateBuffer(new BufferDescription(ShaderResourceLayouts.FrameConstantsData.SizeInBytes, BufferUsage.Uniform | BufferUsage.Dynamic));
        _defaultSampler = _device.Factory.CreateSampler(SamplerDescription.LinearWrap);
        _clampSampler = _device.Factory.CreateSampler(new SamplerDescription(SamplerFilter.Linear, SamplerAddressMode.Clamp));

        ReadOnlySpan<byte> whitePixel = [255, 255, 255, 255];
        _whiteTexture = _device.Factory.CreateTexture(new TextureDescription(1, 1, TextureFormat.Rgba8UNorm, TextureUsage.Sampled), whitePixel);

        Span<byte> onePixel = stackalloc byte[4];
        MemoryMarshal.Write(onePixel, 1f);
        _placeholderR32Texture = _device.Factory.CreateTexture(new TextureDescription(1, 1, TextureFormat.R32Float, TextureUsage.Sampled), onePixel);

        _shadowResourceLayout = _device.Factory.CreateResourceLayout(ShaderResourceLayouts.Shadow);
        _shadowConstantsBuffer = _device.Factory.CreateBuffer(new BufferDescription(ShaderResourceLayouts.ShadowConstantsData.SizeInBytes, BufferUsage.Uniform | BufferUsage.Dynamic));

        _prepassResourceLayout = _device.Factory.CreateResourceLayout(ShaderResourceLayouts.Prepass);
        _prepassConstantsBuffer = _device.Factory.CreateBuffer(new BufferDescription(ShaderResourceLayouts.PrepassConstantsData.SizeInBytes, BufferUsage.Uniform | BufferUsage.Dynamic));

        _ssaoResourceLayout = _device.Factory.CreateResourceLayout(ShaderResourceLayouts.SSAO);
        _ssaoConstantsBuffer = _device.Factory.CreateBuffer(new BufferDescription(ShaderResourceLayouts.SSAOConstantsData.SizeInBytes, BufferUsage.Uniform | BufferUsage.Dynamic));

        _blurResourceLayout = _device.Factory.CreateResourceLayout(ShaderResourceLayouts.Blur);
        _blurConstantsBuffer = _device.Factory.CreateBuffer(new BufferDescription(ShaderResourceLayouts.BlurConstantsData.SizeInBytes, BufferUsage.Uniform | BufferUsage.Dynamic));

        _compositeResourceLayout = _device.Factory.CreateResourceLayout(ShaderResourceLayouts.Composite);
        _compositeConstantsBuffer = _device.Factory.CreateBuffer(new BufferDescription(ShaderResourceLayouts.CompositeConstantsData.SizeInBytes, BufferUsage.Uniform | BufferUsage.Dynamic));

        _fxaaResourceLayout = _device.Factory.CreateResourceLayout(ShaderResourceLayouts.FXAA);
        _fxaaConstantsBuffer = _device.Factory.CreateBuffer(new BufferDescription(ShaderResourceLayouts.FXAAConstantsData.SizeInBytes, BufferUsage.Uniform | BufferUsage.Dynamic));

        var vertexBytes = MemoryMarshal.AsBytes(PostProcessVertex.FullscreenTriangleVertices.AsSpan());
        _fullscreenVertexBuffer = _device.Factory.CreateBuffer(new BufferDescription((uint)vertexBytes.Length, BufferUsage.Vertex), vertexBytes);
        var indexBytes = MemoryMarshal.AsBytes(PostProcessVertex.FullscreenTriangleIndices.AsSpan());
        _fullscreenIndexBuffer = _device.Factory.CreateBuffer(new BufferDescription((uint)indexBytes.Length, BufferUsage.Index), indexBytes);
    }

    private static ReadOnlySpan<byte> AsBytes<T>(ref T value) where T : unmanaged =>
        MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref value, 1));

    public void Dispose()
    {
        foreach (var (vertexBuffer, indexBuffer, _) in _meshCache.Values)
        {
            vertexBuffer.Dispose();
            indexBuffer.Dispose();
        }
        foreach (var pipeline in _pipelineCache.Values) pipeline.Dispose();
        foreach (var pipeline in _passPipelineCache.Values) pipeline.Dispose();
        foreach (var buffer in _drawConstantsCache.Values) buffer.Dispose();
        foreach (var (buffer, _) in _instanceBufferCache.Values) buffer.Dispose();
        foreach (var target in _targetCache.Values) target.Dispose();

        _resourceLayout?.Dispose();
        _frameConstantsBuffer?.Dispose();
        _whiteTexture?.Dispose();
        _placeholderR32Texture?.Dispose();
        _defaultSampler?.Dispose();
        _clampSampler?.Dispose();

        _shadowResourceLayout?.Dispose();
        _shadowConstantsBuffer?.Dispose();
        _prepassResourceLayout?.Dispose();
        _prepassConstantsBuffer?.Dispose();
        _ssaoResourceLayout?.Dispose();
        _ssaoConstantsBuffer?.Dispose();
        _blurResourceLayout?.Dispose();
        _blurConstantsBuffer?.Dispose();
        _compositeResourceLayout?.Dispose();
        _compositeConstantsBuffer?.Dispose();
        _fxaaResourceLayout?.Dispose();
        _fxaaConstantsBuffer?.Dispose();

        _fullscreenVertexBuffer?.Dispose();
        _fullscreenIndexBuffer?.Dispose();
    }
}
