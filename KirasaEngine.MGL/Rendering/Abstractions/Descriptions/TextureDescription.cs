namespace KirasaEngine.MGL.Rendering.Abstractions.Descriptions;

public readonly struct TextureDescription(uint width, uint height, TextureFormat format, TextureUsage usage, uint mipLevels = 1)
{
    public readonly uint Width = width;
    public readonly uint Height = height;
    public readonly TextureFormat Format = format;
    public readonly TextureUsage Usage = usage;
    public readonly uint MipLevels = mipLevels;
}
