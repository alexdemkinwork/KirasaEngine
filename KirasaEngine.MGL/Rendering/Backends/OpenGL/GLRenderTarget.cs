using Silk.NET.OpenGL;

using KirasaEngine.MGL.Rendering;

namespace KirasaEngine.MGL.Rendering.Backends.OpenGL;

internal sealed class GLRenderTarget : IRenderTarget
{
    private readonly GL _gl;

    public uint FramebufferHandle { get; }
    public uint Width { get; }
    public uint Height { get; }
    public TextureFormat ColorFormat { get; }
    public ITexture ColorTexture { get; }
    public ITexture? DepthTexture { get; }

    public GLRenderTarget(GL gl, in RenderTargetDescription description)
    {
        _gl = gl;
        Width = description.Width;
        Height = description.Height;
        ColorFormat = description.ColorFormat;

        ColorTexture = new GLTexture(gl, new TextureDescription(Width, Height, ColorFormat, TextureUsage.RenderTarget | TextureUsage.Sampled), default);

        if (description.DepthFormat is { } depthFormat)
            DepthTexture = new GLTexture(gl, new TextureDescription(Width, Height, depthFormat, TextureUsage.DepthStencil), default);

        FramebufferHandle = _gl.GenFramebuffer();
        GLErrorChecker.ValidateHandle(FramebufferHandle, "Framebuffer");
        GLErrorChecker.CheckError(_gl, "GenFramebuffer");
        
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, FramebufferHandle);
        GLErrorChecker.CheckError(_gl, "BindFramebuffer");
        
        _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, ((GLTexture)ColorTexture).Handle, 0);
        GLErrorChecker.CheckError(_gl, "FramebufferTexture2D Color");

        if (DepthTexture is not null)
        {
            var attachment = description.DepthFormat == TextureFormat.Depth32Float
                ? FramebufferAttachment.DepthAttachment
                : FramebufferAttachment.DepthStencilAttachment;
            _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, attachment, TextureTarget.Texture2D, ((GLTexture)DepthTexture).Handle, 0);
            GLErrorChecker.CheckError(_gl, "FramebufferTexture2D Depth");
        }

        var status = _gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        GLErrorChecker.CheckError(_gl, "CheckFramebufferStatus");
        if (status != GLEnum.FramebufferComplete)
        {
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            throw new InvalidOperationException($"OpenGL framebuffer incomplete: {status} (ColorFormat={ColorFormat}, ColorTexture={ColorTexture?.Width}x{ColorTexture?.Height}, DepthTexture={DepthTexture?.Width}x{DepthTexture?.Height})");
        }

        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        GLErrorChecker.CheckError(_gl, "BindFramebuffer reset");
    }

    public void Dispose()
    {
        GLErrorChecker.ValidateHandle(FramebufferHandle, "Framebuffer");
        _gl.DeleteFramebuffer(FramebufferHandle);
        GLErrorChecker.CheckError(_gl, "DeleteFramebuffer");
        ColorTexture.Dispose();
        DepthTexture?.Dispose();
    }
}
