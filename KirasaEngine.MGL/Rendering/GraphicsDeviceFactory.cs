using KirasaEngine.MGL.Rendering.Backends.OpenGL;

namespace KirasaEngine.MGL.Rendering;

public static class GraphicsDeviceFactory
{
    /// <summary>
    /// Creates a window configured for the given backend. OpenGL needs its context requested up front
    /// (ContextAPI.OpenGL, core profile); the other three backends draw through their own native APIs and
    /// only need the window for its native handle (Win32 HWND / VkSurface), so they request no GL context.
    /// </summary>
    public static IWindow CreateWindow(GraphicsBackend backend, uint width, uint height, string title = "KirasaEngine.MGL", bool visible = true)
    {
        var options = WindowOptions.Default;
        options.Size = new Vector2D<int>((int)width, (int)height);
        options.Title = title;
        options.IsVisible = visible;
        options.API = backend == GraphicsBackend.OpenGL
            ? new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.Default, new APIVersion(4, 6))
            : GraphicsAPI.None;
        return Window.Create(options);
    }

    public static IGraphicsDevice Create(GraphicsBackend backend) => backend switch
    {
        GraphicsBackend.OpenGL => new GLGraphicsDevice(),
        GraphicsBackend.Direct3D11 => new Backends.Direct3D11.D3D11GraphicsDevice(),
        GraphicsBackend.Direct3D12 => new Backends.Direct3D12.D3D12GraphicsDevice(),
        GraphicsBackend.Vulkan => new Backends.Vulkan.VulkanGraphicsDevice(),
        _ => throw new ArgumentOutOfRangeException(nameof(backend)),
    };
}
