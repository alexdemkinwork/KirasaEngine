namespace KirasaEngine.MGL.Rendering.Abstractions;

public interface ITexture : IDisposable
{
    uint Width { get; }
    uint Height { get; }
    TextureFormat Format { get; }
    TextureUsage Usage { get; }
}
