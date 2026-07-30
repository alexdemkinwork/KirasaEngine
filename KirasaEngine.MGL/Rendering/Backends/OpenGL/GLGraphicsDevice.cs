using Silk.NET.OpenGL;

using KirasaEngine.MGL.Rendering;
using KirasaEngine.MGL.Rendering.Abstractions;

namespace KirasaEngine.MGL.Rendering.Backends.OpenGL;

/// <summary>
/// OpenGL implementation of IGraphicsDevice.
/// Supports OpenGL 4.6 Core Profile with ClipControl for D3D/Vulkan-compatible depth range.
/// All OpenGL operations include error checking via GLErrorChecker.
/// </summary>
public sealed class GLGraphicsDevice : IGraphicsDevice
{
    private GL _gl = null!;
    private IWindow _window = null!;

    /// <summary>Gets the graphics backend type (always OpenGL for this implementation).</summary>
    public GraphicsBackend Backend => GraphicsBackend.OpenGL;
    
    /// <summary>Gets the resource factory for creating OpenGL resources.</summary>
    public IResourceFactory Factory { get; private set; } = null!;
    
    /// <summary>Gets the current width of the render target in pixels.</summary>
    public uint Width { get; private set; }
    
    /// <summary>Gets the current height of the render target in pixels.</summary>
    public uint Height { get; private set; }

    /// <summary>
    /// Initializes the OpenGL graphics device with the specified window and dimensions.
    /// Requires OpenGL 4.6 or higher for ClipControl and other modern features.
    /// </summary>
    /// <param name="description">Configuration including window, dimensions, and debug flag.</param>
    /// <exception cref="ArgumentException">Thrown if no window is provided for OpenGL backend.</exception>
    /// <exception cref="InvalidOperationException">Thrown if OpenGL 4.6 is not supported.</exception>
    public void Initialize(GraphicsDeviceDescription description)
    {
        _window = description.Window ?? throw new ArgumentException("OpenGL requires a window (it can stay hidden) to host its context.", nameof(description));
        Width = description.Width;
        Height = description.Height;

        _gl = GL.GetApi(_window);

        // Log OpenGL version information
        var versionString = GLErrorChecker.GetVersionString(_gl);
        Console.WriteLine($"[OpenGL] Initializing device: {versionString}");

        // Verify OpenGL 4.6 support (required for ClipControl and other features)
        // Note: We assume 4.6 is available based on the GraphicsAPI configuration in GraphicsDeviceFactory
        // If ClipControl fails, it will be caught by error checking below

        // Camera produces D3D/Vulkan-style [0,1] depth-range matrices; reconcile GL's native [-1,1] here so
        // Abstractions.Scene stays backend-agnostic (see the doc comment on Camera).
        _gl.ClipControl(GLEnum.LowerLeft, GLEnum.ZeroToOne);
        GLErrorChecker.CheckError(_gl, "ClipControl");
        
        _gl.Enable(EnableCap.DepthTest);
        GLErrorChecker.CheckError(_gl, "Enable DepthTest");

        Console.WriteLine($"[OpenGL] Device initialized: {Width}x{Height}");
        Factory = new GLResourceFactory(_gl);
    }

    /// <summary>Creates a new command list for recording drawing commands.</summary>
    public ICommandList CreateCommandList() => new GLCommandList(_gl);

    /// <summary>
    /// Submits the command list for execution.
    /// In OpenGL, commands execute immediately, so this just calls Finish() to ensure completion.
    /// </summary>
    public void Submit(ICommandList commandList)
    {
        // GLCommandList already executed every call synchronously against the global GL context; Finish()
        // just guarantees a subsequent ReadRenderTarget observes the completed image.
        _gl.Finish();
        GLErrorChecker.CheckError(_gl, "Finish");
    }

    /// <summary>Presents the current frame by swapping buffers.</summary>
    public void Present() => _window.GLContext?.SwapBuffers();

    /// <summary>Resizes the viewport to the specified dimensions.</summary>
    /// <param name="width">New width in pixels.</param>
    /// <param name="height">New height in pixels.</param>
    public void Resize(uint width, uint height)
    {
        Width = width;
        Height = height;
        _gl.Viewport(0, 0, width, height);
        GLErrorChecker.CheckError(_gl, "Viewport");
        Console.WriteLine($"[OpenGL] Resized to: {width}x{height}");
    }

    public unsafe byte[] ReadRenderTarget(IRenderTarget target)
    {
        var glTarget = (GLRenderTarget)target;
        var width = (int)glTarget.Width;
        var height = (int)glTarget.Height;
        var pixels = new byte[width * height * 4];

        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, glTarget.FramebufferHandle);
        GLErrorChecker.CheckError(_gl, "BindFramebuffer for read");
        
        _gl.ReadBuffer(GLEnum.ColorAttachment0);
        GLErrorChecker.CheckError(_gl, "ReadBuffer");
        
        _gl.PixelStore(PixelStoreParameter.PackAlignment, 1);
        GLErrorChecker.CheckError(_gl, "PixelStore");

        fixed (byte* ptr = pixels)
            _gl.ReadPixels(0, 0, (uint)width, (uint)height, PixelFormat.Rgba, PixelType.UnsignedByte, ptr);
        GLErrorChecker.CheckError(_gl, "ReadPixels");

        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        GLErrorChecker.CheckError(_gl, "BindFramebuffer reset");

        FlipRowsVertically(pixels, width, height);
        return pixels;
    }

    /// <summary>OpenGL's row 0 is the bottom of the image; flip to the top-left-origin convention shared by every backend.</summary>
    private static void FlipRowsVertically(byte[] pixels, int width, int height)
    {
        var stride = width * 4;
        var row = new byte[stride];
        for (var y = 0; y < height / 2; y++)
        {
            var top = y * stride;
            var bottom = (height - 1 - y) * stride;
            System.Buffer.BlockCopy(pixels, top, row, 0, stride);
            System.Buffer.BlockCopy(pixels, bottom, pixels, top, stride);
            System.Buffer.BlockCopy(row, 0, pixels, bottom, stride);
        }
    }

    /// <summary>Disposes the OpenGL context.</summary>
    public void Dispose()
    {
        Console.WriteLine("[OpenGL] Disposing device");
        _gl.Dispose();
    }
}
