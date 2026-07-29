using Silk.NET.OpenGL;

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
        _gl.BindTexture(TextureTarget.Texture2D, Handle);

        if (!initialData.IsEmpty)
        {
            fixed (byte* ptr = initialData)
                _gl.TexImage2D(TextureTarget.Texture2D, 0, (int)internalFormat, Width, Height, 0, pixelFormat, pixelType, ptr);
        }
        else
        {
            _gl.TexImage2D(TextureTarget.Texture2D, 0, (int)internalFormat, Width, Height, 0, pixelFormat, pixelType, null);
        }

        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);

        _gl.BindTexture(TextureTarget.Texture2D, 0);
    }

    public void Dispose() => _gl.DeleteTexture(Handle);
}
