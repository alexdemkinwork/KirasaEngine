using Silk.NET.OpenGL;

using KirasaEngine.MGL.Rendering;

namespace KirasaEngine.MGL.Rendering.Backends.OpenGL;

internal sealed class GLTexture : ITexture
{
    private readonly GL _gl;

    public uint Handle { get; }
    public uint Width { get; }
    public uint Height { get; }
    public TextureFormat Format { get; }
    public TextureUsage Usage { get; }

    public unsafe GLTexture(GL gl, in TextureDescription description, ReadOnlySpan<byte> initialData)
    {
        _gl = gl;
        Width = description.Width;
        Height = description.Height;
        Format = description.Format;
        Usage = description.Usage;

        var (internalFormat, pixelFormat, pixelType) = GLFormats.MapTexture(description.Format);

        Handle = _gl.GenTexture();
        GLErrorChecker.ValidateHandle(Handle, "Texture");
        GLErrorChecker.CheckError(_gl, "GenTexture");
        
        _gl.BindTexture(TextureTarget.Texture2D, Handle);
        GLErrorChecker.CheckError(_gl, "BindTexture");

        if (!initialData.IsEmpty)
        {
            fixed (byte* ptr = initialData)
                _gl.TexImage2D(TextureTarget.Texture2D, 0, (int)internalFormat, Width, Height, 0, pixelFormat, pixelType, ptr);
            GLErrorChecker.CheckError(_gl, "TexImage2D with initial data");
        }
        else
        {
            _gl.TexImage2D(TextureTarget.Texture2D, 0, (int)internalFormat, Width, Height, 0, pixelFormat, pixelType, null);
            GLErrorChecker.CheckError(_gl, "TexImage2D without initial data");
        }

        // Set default texture parameters based on usage
        // These will be overridden by sampler parameters when a sampler is bound
        var minFilter = description.Usage.HasFlag(TextureUsage.RenderTarget) 
            ? TextureMinFilter.Nearest 
            : TextureMinFilter.Linear;
        var magFilter = description.Usage.HasFlag(TextureUsage.RenderTarget) 
            ? TextureMagFilter.Nearest 
            : TextureMagFilter.Linear;
        var wrapMode = GLEnum.ClampToEdge; // Default to ClampToEdge for safety
        
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)minFilter);
        GLErrorChecker.CheckError(_gl, "TexParameter MinFilter");
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)magFilter);
        GLErrorChecker.CheckError(_gl, "TexParameter MagFilter");
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)wrapMode);
        GLErrorChecker.CheckError(_gl, "TexParameter WrapS");
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)wrapMode);
        GLErrorChecker.CheckError(_gl, "TexParameter WrapT");

        // Set base level and max level for completeness
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBaseLevel, 0);
        GLErrorChecker.CheckError(_gl, "TexParameter BaseLevel");
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, 0);
        GLErrorChecker.CheckError(_gl, "TexParameter MaxLevel");

        _gl.BindTexture(TextureTarget.Texture2D, 0);
        GLErrorChecker.CheckError(_gl, "BindTexture reset");
    }

    public void Dispose()
    {
        GLErrorChecker.ValidateHandle(Handle, "Texture");
        _gl.DeleteTexture(Handle);
        GLErrorChecker.CheckError(_gl, "DeleteTexture");
    }
}
