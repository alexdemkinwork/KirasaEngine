using Silk.NET.OpenGL;

using KirasaEngine.MGL.Rendering;

namespace KirasaEngine.MGL.Rendering.Backends.OpenGL;

public sealed class GLGraphicsDevice : IGraphicsDevice
{
    private GL _gl = null!;
    private IWindow _window = null!;

    public GraphicsBackend Backend => GraphicsBackend.OpenGL;
    public IResourceFactory Factory { get; private set; } = null!;
    public uint Width { get; private set; }
    public uint Height { get; private set; }

    public void Initialize(GraphicsDeviceDescription description)
    {
        _window = description.Window ?? throw new ArgumentException("OpenGL requires a window (it can stay hidden) to host its context.", nameof(description));
        Width = description.Width;
        Height = description.Height;

        _gl = GL.GetApi(_window);

        // Camera produces D3D/Vulkan-style [0,1] depth-range matrices; reconcile GL's native [-1,1] here so
        // Abstractions.Scene stays backend-agnostic (see the doc comment on Camera).
        _gl.ClipControl(GLEnum.LowerLeft, GLEnum.ZeroToOne);
        _gl.Enable(EnableCap.DepthTest);

        Factory = new GLResourceFactory(_gl);
    }

    public ICommandList CreateCommandList() => new GLCommandList(_gl);

    public void Submit(ICommandList commandList)
    {
        // GLCommandList already executed every call synchronously against the global GL context; Finish()
        // just guarantees a subsequent ReadRenderTarget observes the completed image.
        _gl.Finish();
    }

    public void Present() => _window.GLContext?.SwapBuffers();

    public void Resize(uint width, uint height)
    {
        Width = width;
        Height = height;
        _gl.Viewport(0, 0, width, height);
    }

    public unsafe byte[] ReadRenderTarget(IRenderTarget target)
    {
        var glTarget = (GLRenderTarget)target;
        var width = (int)glTarget.Width;
        var height = (int)glTarget.Height;
        var pixels = new byte[width * height * 4];

        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, glTarget.FramebufferHandle);
        _gl.ReadBuffer(GLEnum.ColorAttachment0);
        _gl.PixelStore(PixelStoreParameter.PackAlignment, 1);

        fixed (byte* ptr = pixels)
            _gl.ReadPixels(0, 0, (uint)width, (uint)height, PixelFormat.Rgba, PixelType.UnsignedByte, ptr);

        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

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

    public void Dispose() => _gl.Dispose();
}
