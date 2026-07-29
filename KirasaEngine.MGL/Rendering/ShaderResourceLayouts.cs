namespace KirasaEngine.MGL.Rendering;

/// <summary>
/// Canonical resource-binding scheme for the "Standard" built-in shader.
///
/// <para>OpenGL and Direct3D11/Direct3D12 use the numeric <see cref="ResourceLayoutElementDescription.Binding"/>
/// value on each element directly, scoped per resource kind (HLSL's b/t/s register spaces are independent,
/// and GL's uniform-buffer-binding-points vs. texture-unit namespaces are likewise independent of each other)
/// — see GLSL/Standard.* and HLSL/Standard.hlsl, whose layout(binding=N)/register(_N) numbers match the
/// constants below one-to-one.</para>
///
/// <para><b>Vulkan is the exception</b>: a descriptor set has one flat binding-number space shared by every
/// resource kind, so reusing 0 for both FrameConstants (a UBO) and BaseColorTexture (a sampler) — as the
/// constants below do, since that's valid for GL/D3D — would collide. The Vulkan backend must therefore
/// ignore this file's Binding values for its own VkDescriptorSetLayoutBinding/GLSL binding numbers and use
/// each element's position within <see cref="Standard"/>.Elements instead (0, 1, 2, 3) — exactly what
/// VulkanGLSL/Standard.vert/frag already declare.</para>
/// </summary>
public static class ShaderResourceLayouts
{
    public const uint FrameConstantsBinding = 0;
    public const uint DrawConstantsBinding = 1;
    public const uint BaseColorTextureBinding = 0;
    public const uint BaseColorSamplerBinding = 0;
    public const uint ShadowMapTextureBinding = 1;
    public const uint ShadowMapSamplerBinding = 1;
    public const uint AOTextureBinding = 2;
    public const uint AOSamplerBinding = 2;

    public static ResourceLayoutDescription Standard { get; } = new()
    {
        Elements =
        [
            new ResourceLayoutElementDescription("FrameConstants", ResourceKind.UniformBuffer, ShaderStage.Vertex | ShaderStage.Fragment, FrameConstantsBinding),
            new ResourceLayoutElementDescription("DrawConstants", ResourceKind.UniformBuffer, ShaderStage.Fragment, DrawConstantsBinding),
            new ResourceLayoutElementDescription("BaseColorTexture", ResourceKind.TextureReadOnly, ShaderStage.Fragment, BaseColorTextureBinding),
            new ResourceLayoutElementDescription("BaseColorSampler", ResourceKind.Sampler, ShaderStage.Fragment, BaseColorSamplerBinding),
            new ResourceLayoutElementDescription("ShadowMapTexture", ResourceKind.TextureReadOnly, ShaderStage.Fragment, ShadowMapTextureBinding),
            new ResourceLayoutElementDescription("ShadowMapSampler", ResourceKind.Sampler, ShaderStage.Fragment, ShadowMapSamplerBinding),
            new ResourceLayoutElementDescription("AOTexture", ResourceKind.TextureReadOnly, ShaderStage.Fragment, AOTextureBinding),
            new ResourceLayoutElementDescription("AOSampler", ResourceKind.Sampler, ShaderStage.Fragment, AOSamplerBinding),
        ],
    };

    [StructLayout(LayoutKind.Sequential)]
    public struct FrameConstantsData
    {
        /// <summary>
        /// Uploaded byte-for-byte from System.Numerics (row-major); GLSL/HLSL read the same bytes as
        /// column-major, which performs the row-vector -> column-vector convention switch for free — see
        /// the comment in SceneRenderer.UpdateFrameConstants. Do not transpose before upload.
        /// </summary>
        public Matrix4x4 ViewProjection;

        /// <summary>Same convention as <see cref="ViewProjection"/>; transforms world position into the shadow map's light-clip-space.</summary>
        public Matrix4x4 LightViewProjection;

        public Vector4 LightDirection;

        /// <summary>rgb = color, a = intensity.</summary>
        public Vector4 LightColor;
        public Vector4 AmbientColor;

        /// <summary>x = 1/shadow map resolution (texel size), y = depth bias, z = PCF half-radius in taps (0/1/2), w = shadows enabled (0/1).</summary>
        public Vector4 ShadowParams;

        /// <summary>x = render target width, y = render target height (for reconstructing this fragment's screen UV), z = AO enabled (0/1), w = unused.</summary>
        public Vector4 ScreenParams;

        public const uint SizeInBytes = 64 + 64 + 16 * 5;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DrawConstantsData
    {
        public Vector4 BaseColor;

        public const uint SizeInBytes = 16;
    }

    // ---- Shadow depth pass: renders scene geometry from the light's point of view into an R32Float target
    // (see SceneRenderer's shadow pass) storing post-projection depth as a plain color value, sidestepping
    // every backend's separate "sampleable depth texture" plumbing entirely. ----
    public static ResourceLayoutDescription Shadow { get; } = new()
    {
        Elements = [new ResourceLayoutElementDescription("ShadowConstants", ResourceKind.UniformBuffer, ShaderStage.Vertex, 0)],
    };

    [StructLayout(LayoutKind.Sequential)]
    public struct ShadowConstantsData
    {
        public Matrix4x4 LightViewProjection;
        public const uint SizeInBytes = 64;
    }

    // ---- Depth+normal prepass: renders scene geometry from the main camera into an Rgba16Float target
    // (rgb = view-space normal, a = view-space linear depth) so SSAO has something to sample without a full
    // deferred G-buffer (see SceneRenderer's prepass). ----
    public static ResourceLayoutDescription Prepass { get; } = new()
    {
        Elements = [new ResourceLayoutElementDescription("PrepassConstants", ResourceKind.UniformBuffer, ShaderStage.Vertex, 0)],
    };

    [StructLayout(LayoutKind.Sequential)]
    public struct PrepassConstantsData
    {
        public Matrix4x4 ViewProjection;
        public Matrix4x4 View;
        public const uint SizeInBytes = 128;
    }

    // ---- SSAO: fullscreen pass reading the prepass output, writing a single-channel occlusion term. ----
    public static ResourceLayoutDescription SSAO { get; } = new()
    {
        Elements =
        [
            new ResourceLayoutElementDescription("SSAOConstants", ResourceKind.UniformBuffer, ShaderStage.Fragment, 0),
            new ResourceLayoutElementDescription("NormalDepthTexture", ResourceKind.TextureReadOnly, ShaderStage.Fragment, 0),
            new ResourceLayoutElementDescription("NormalDepthSampler", ResourceKind.Sampler, ShaderStage.Fragment, 0),
        ],
    };

    [StructLayout(LayoutKind.Sequential)]
    public struct SSAOConstantsData
    {
        /// <summary>x = tan(verticalFovRadians/2), y = aspect ratio (width/height), z = sample radius (world units), w = occlusion power/contrast.</summary>
        public Vector4 Params0;

        /// <summary>x = sample count (0..48, as a float — indexes into the shader's fixed 48-entry hemisphere kernel), y = depth bias, z/w unused.</summary>
        public Vector4 Params1;

        public const uint SizeInBytes = 32;
    }

    // ---- Separable blur: reused for both the bright-pass+horizontal-blur invocation and the plain
    // vertical-blur invocation (Params1.y toggles the threshold), so bloom needs only one extra shader. ----
    public static ResourceLayoutDescription Blur { get; } = new()
    {
        Elements =
        [
            new ResourceLayoutElementDescription("BlurConstants", ResourceKind.UniformBuffer, ShaderStage.Fragment, 0),
            new ResourceLayoutElementDescription("SourceTexture", ResourceKind.TextureReadOnly, ShaderStage.Fragment, 0),
            new ResourceLayoutElementDescription("SourceSampler", ResourceKind.Sampler, ShaderStage.Fragment, 0),
        ],
    };

    [StructLayout(LayoutKind.Sequential)]
    public struct BlurConstantsData
    {
        /// <summary>x = texel size X, y = texel size Y, z = 0 for a horizontal pass / 1 for vertical, w = blur half-radius in texels (as a float).</summary>
        public Vector4 Params0;

        /// <summary>x = bright-pass threshold, y = apply the threshold this invocation (0/1) — only the first (horizontal) pass thresholds, z/w unused.</summary>
        public Vector4 Params1;

        public const uint SizeInBytes = 32;
    }

    // ---- Composite: HDR scene color + blurred bloom -> ACES tonemap -> saturation/contrast -> vignette. ----
    public static ResourceLayoutDescription Composite { get; } = new()
    {
        Elements =
        [
            new ResourceLayoutElementDescription("CompositeConstants", ResourceKind.UniformBuffer, ShaderStage.Fragment, 0),
            new ResourceLayoutElementDescription("HdrColorTexture", ResourceKind.TextureReadOnly, ShaderStage.Fragment, 0),
            new ResourceLayoutElementDescription("HdrColorSampler", ResourceKind.Sampler, ShaderStage.Fragment, 0),
            new ResourceLayoutElementDescription("BloomTexture", ResourceKind.TextureReadOnly, ShaderStage.Fragment, 1),
            new ResourceLayoutElementDescription("BloomSampler", ResourceKind.Sampler, ShaderStage.Fragment, 1),
        ],
    };

    [StructLayout(LayoutKind.Sequential)]
    public struct CompositeConstantsData
    {
        /// <summary>x = bloom intensity, y = vignette intensity, z = saturation, w = contrast.</summary>
        public Vector4 Params0;

        /// <summary>x = bloom enabled (0/1), y = vignette enabled (0/1), z/w unused.</summary>
        public Vector4 Params1;

        public const uint SizeInBytes = 32;
    }

    // ---- FXAA: final antialiasing pass, reads the composite pass's LDR output. ----
    public static ResourceLayoutDescription FXAA { get; } = new()
    {
        Elements =
        [
            new ResourceLayoutElementDescription("FXAAConstants", ResourceKind.UniformBuffer, ShaderStage.Fragment, 0),
            new ResourceLayoutElementDescription("SourceTexture", ResourceKind.TextureReadOnly, ShaderStage.Fragment, 0),
            new ResourceLayoutElementDescription("SourceSampler", ResourceKind.Sampler, ShaderStage.Fragment, 0),
        ],
    };

    [StructLayout(LayoutKind.Sequential)]
    public struct FXAAConstantsData
    {
        /// <summary>x = texel size X, y = texel size Y, z/w unused.</summary>
        public Vector4 Params0;

        public const uint SizeInBytes = 16;
    }
}
