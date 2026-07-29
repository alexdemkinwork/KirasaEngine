namespace KirasaEngine.MGL.Rendering.Abstractions;

/// <summary>An offscreen (or backbuffer-backed) color+depth attachment set that can be drawn into and read back.</summary>
public interface IRenderTarget : IDisposable
{
    uint Width { get; }
    uint Height { get; }
    TextureFormat ColorFormat { get; }
    ITexture ColorTexture { get; }
    ITexture? DepthTexture { get; }
}
