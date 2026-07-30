using System.Numerics;

namespace KirasaEngine.MGL.Rendering.RenderGraph;

/// <summary>
/// Provides context for a render pass, including access to resources, scene data, and rendering utilities.
/// </summary>
public class RenderContext
{
    /// <summary>
    /// Gets the graphics device.
    /// </summary>
    public IGraphicsDevice Device { get; }
    
    /// <summary>
    /// Gets the scene being rendered.
    /// </summary>
    public Scene Scene { get; }
    
    /// <summary>
    /// Gets the resource manager for GPU resources.
    /// </summary>
    public ResourceManager ResourceManager { get; }
    
    /// <summary>
    /// Gets the shader compiler.
    /// </summary>
    public ShaderCompiler ShaderCompiler { get; }
    
    /// <summary>
    /// Gets the current camera.
    /// </summary>
    public Camera Camera { get; }
    
    /// <summary>
    /// Gets the camera's transform.
    /// </summary>
    public Transform CameraTransform { get; }
    
    /// <summary>
    /// Gets the primary light.
    /// </summary>
    public SceneNode? LightNode { get; }
    
    /// <summary>
    /// Gets the render target width.
    /// </summary>
    public uint Width { get; }
    
    /// <summary>
    /// Gets the render target height.
    /// </summary>
    public uint Height { get; }
    
    /// <summary>
    /// Gets the post-process settings.
    /// </summary>
    public PostProcessSettings Settings { get; }
    
    /// <summary>
    /// Initializes a new instance of the <see cref="RenderContext"/> class.
    /// </summary>
    /// <param name="device">The graphics device.</param>
    /// <param name="scene">The scene being rendered.</param>
    /// <param name="resourceManager">The resource manager.</param>
    /// <param name="shaderCompiler">The shader compiler.</param>
    /// <param name="camera">The current camera.</param>
    /// <param name="cameraTransform">The camera's transform.</param>
    /// <param name="lightNode">The primary light.</param>
    /// <param name="width">The render target width.</param>
    /// <param name="height">The render target height.</param>
    /// <param name="settings">The post-process settings.</param>
    public RenderContext(
        IGraphicsDevice device,
        Scene scene,
        ResourceManager resourceManager,
        ShaderCompiler shaderCompiler,
        Camera camera,
        Transform cameraTransform,
        SceneNode? lightNode,
        uint width,
        uint height,
        PostProcessSettings settings)
    {
        Device = device;
        Scene = scene;
        ResourceManager = resourceManager;
        ShaderCompiler = shaderCompiler;
        Camera = camera;
        CameraTransform = cameraTransform;
        LightNode = lightNode;
        Width = width;
        Height = height;
        Settings = settings;
    }
    
    /// <summary>
    /// Gets the view-projection matrix for the camera.
    /// </summary>
    public Matrix4x4 GetViewProjectionMatrix()
    {
        var view = Camera.GetViewMatrix(CameraTransform);
        var projection = Camera.GetProjectionMatrix(Width / (float)Height);
        return view * projection;
    }
    
    /// <summary>
    /// Gets the light's view-projection matrix for shadow mapping.
    /// </summary>
    public Matrix4x4 GetLightViewProjectionMatrix()
    {
        if (LightNode == null)
            return Matrix4x4.Identity;
        
        var lightDirection = Vector3.Normalize(LightNode.Transform.Forward);
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        var any = false;
        
        foreach (var node in Scene.Traverse())
        {
            if (node.Renderer == null) continue;
            var p = node.Transform.WorldPosition;
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
            any = true;
        }
        
        foreach (var batch in Scene.InstancedBatches)
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
        
        var padding = new Vector3(Settings.ShadowFrustumPadding);
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
}