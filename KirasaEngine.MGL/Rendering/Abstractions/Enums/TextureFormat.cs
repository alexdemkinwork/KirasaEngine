namespace KirasaEngine.MGL.Rendering.Abstractions.Enums;

public enum TextureFormat
{
    Rgba8UNorm,
    Bgra8UNorm,
    R32Float,
    /// <summary>HDR color format: scene color pre-tonemap and bloom intermediate targets, so values above 1.0 survive until the composite pass.</summary>
    Rgba16Float,
    Depth24Stencil8,
    Depth32Float,
}

[Flags]
public enum TextureUsage
{
    Sampled = 1 << 0,
    RenderTarget = 1 << 1,
    DepthStencil = 1 << 2,
}
