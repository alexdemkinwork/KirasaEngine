namespace KirasaEngine.MGL.Rendering.Abstractions.Descriptions;

public readonly struct RenderTargetDescription(uint width, uint height, TextureFormat colorFormat, TextureFormat? depthFormat = TextureFormat.Depth24Stencil8)
{
    public readonly uint Width = width;
    public readonly uint Height = height;
    public readonly TextureFormat ColorFormat = colorFormat;
    public readonly TextureFormat? DepthFormat = depthFormat;
}
