using Silk.NET.Direct3D11;

using KirasaEngine.MGL.Rendering;

namespace KirasaEngine.MGL.Rendering.Backends.Direct3D11;

internal sealed unsafe class D3D11RenderTarget : IRenderTarget
{
    public uint Width { get; }
    public uint Height { get; }
    public TextureFormat ColorFormat { get; }
    public ITexture ColorTexture { get; }
    public ITexture? DepthTexture { get; }

    public ID3D11RenderTargetView* RenderTargetView => ((D3D11Texture)ColorTexture).RenderTargetView;
    public ID3D11DepthStencilView* DepthStencilView =>
        DepthTexture is D3D11Texture depth ? depth.DepthStencilView : null;

    public D3D11RenderTarget(ID3D11Device* device, ID3D11DeviceContext* context, in RenderTargetDescription description)
    {
        Width = description.Width;
        Height = description.Height;
        ColorFormat = description.ColorFormat;

        ColorTexture = new D3D11Texture(
            device,
            context,
            new TextureDescription(Width, Height, ColorFormat, TextureUsage.RenderTarget | TextureUsage.Sampled),
            default);

        if (description.DepthFormat is { } depthFormat)
        {
            DepthTexture = new D3D11Texture(
                device,
                context,
                new TextureDescription(Width, Height, depthFormat, TextureUsage.DepthStencil),
                default);
        }
    }

    public void Dispose()
    {
        ColorTexture.Dispose();
        DepthTexture?.Dispose();
    }
}
