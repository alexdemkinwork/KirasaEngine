namespace KirasaEngine.MGL.Rendering;

using KirasaEngine.MGL.Rendering.RenderGraph;
using KirasaEngine.MGL.Rendering.RenderGraph.Passes;

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
    private readonly RenderGraph.RenderGraph _renderGraph;
    private readonly ResourceManager _resourceManager;
    private readonly ShaderCompiler _shaderCompiler;
    public PostProcessSettings Settings { get; set; }

    public SceneRenderer(IGraphicsDevice device, PostProcessSettings? settings = null)
    {
        _device = device;
        Settings = settings ?? PostProcessSettings.Default;
        _resourceManager = new ResourceManager(device);
        _shaderCompiler = new ShaderCompiler(device);
        _renderGraph = new RenderGraph.RenderGraph(device);
        
        // Initialize render graph
        _renderGraph.AddPass(new ShadowPass());
        _renderGraph.AddPass(new Prepass());
        _renderGraph.AddPass(new SSAOPass());
        _renderGraph.AddPass(new ForwardPass());
        _renderGraph.AddPass(new BloomPass());
        _renderGraph.AddPass(new CompositePass());
        _renderGraph.AddPass(new FXAAPass());
    }

    /// <summary>Renders the scene once into an offscreen target and reads it back as top-left-origin RGBA8 bytes.</summary>
    public byte[] RenderToTexture(Scene scene, uint width, uint height, SceneNode? cameraNode = null)
    {
        cameraNode ??= scene.FindCameraNode() ?? throw new InvalidOperationException("Scene contains no camera node.");
        var camera = cameraNode.Camera!;
        
        var context = new RenderContext(
            _device,
            scene,
            _resourceManager,
            _shaderCompiler,
            camera,
            cameraNode.Transform,
            scene.FindLightNodes().FirstOrDefault(),
            width,
            height,
            Settings,
            _renderGraph);
        
        var cmd = _device.CreateCommandList();
        _renderGraph.Execute(cmd, context);
        
        var finalRenderTarget = _renderGraph.GetRenderTarget(KirasaEngine.MGL.Rendering.RenderGraph.RenderGraphTextureUsage.Final);
        var result = _device.ReadRenderTarget(finalRenderTarget);
        cmd.Dispose();
        
        return result;
    }

    // The new render graph system handles all passes, so the old pass methods are no longer needed.

    private static ReadOnlySpan<byte> AsBytes<T>(ref T value) where T : unmanaged =>
        MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref value, 1));

    public void Dispose()
    {
        _renderGraph.Dispose();
    }
}
