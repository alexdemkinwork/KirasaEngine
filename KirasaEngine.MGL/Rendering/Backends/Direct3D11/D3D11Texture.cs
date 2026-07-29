using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;
using Silk.NET.DXGI;

namespace KirasaEngine.MGL.Rendering.Backends.Direct3D11;

internal sealed unsafe class D3D11Texture : ITexture
{
    public ID3D11Texture2D* Handle;
    public ID3D11ShaderResourceView* ShaderResourceView;
    public ID3D11RenderTargetView* RenderTargetView;
    public ID3D11DepthStencilView* DepthStencilView;

    public uint Width { get; }
    public uint Height { get; }
    public TextureFormat Format { get; }
    public TextureUsage Usage { get; }

    public D3D11Texture(ID3D11Device* device, ID3D11DeviceContext* context, in TextureDescription description, ReadOnlySpan<byte> initialData)
    {
        Width = description.Width;
        Height = description.Height;
        Format = description.Format;
        Usage = description.Usage;

        var mipLevels = description.MipLevels == 0 ? 1u : description.MipLevels;

        var bindFlags = 0u;
        if (description.Usage.HasFlag(TextureUsage.Sampled)) bindFlags |= (uint)BindFlag.ShaderResource;
        if (description.Usage.HasFlag(TextureUsage.RenderTarget)) bindFlags |= (uint)BindFlag.RenderTarget;
        if (description.Usage.HasFlag(TextureUsage.DepthStencil)) bindFlags |= (uint)BindFlag.DepthStencil;

        var desc = new Texture2DDesc
        {
            Width = Width,
            Height = Height,
            MipLevels = mipLevels,
            ArraySize = 1,
            Format = D3D11Formats.MapResource(description.Format, description.Usage),
            SampleDesc = new SampleDesc(1, 0),
            Usage = Silk.NET.Direct3D11.Usage.Default,
            BindFlags = bindFlags,
            CPUAccessFlags = 0,
            MiscFlags = 0,
        };

        ID3D11Texture2D* handle = null;
        if (!initialData.IsEmpty)
        {
            fixed (byte* ptr = initialData)
            {
                var subresource = new SubresourceData
                {
                    PSysMem = ptr,
                    SysMemPitch = Width * D3D11Formats.BytesPerPixel(description.Format),
                    SysMemSlicePitch = 0,
                };
                D3D11GraphicsDevice.Check(device->CreateTexture2D(&desc, &subresource, ref handle), "ID3D11Device::CreateTexture2D");
            }
        }
        else
        {
            D3D11GraphicsDevice.Check(device->CreateTexture2D(&desc, null, ref handle), "ID3D11Device::CreateTexture2D");
        }

        Handle = handle;

        if (description.Usage.HasFlag(TextureUsage.Sampled))
        {
            var srvDesc = new ShaderResourceViewDesc
            {
                Format = D3D11Formats.MapSrv(description.Format),
                ViewDimension = D3DSrvDimension.D3D11SrvDimensionTexture2D,
            };
            srvDesc.Anonymous.Texture2D = new Tex2DSrv { MostDetailedMip = 0, MipLevels = mipLevels };

            ID3D11ShaderResourceView* srv = null;
            D3D11GraphicsDevice.Check(
                device->CreateShaderResourceView((ID3D11Resource*)Handle, &srvDesc, ref srv),
                "ID3D11Device::CreateShaderResourceView");
            ShaderResourceView = srv;
        }

        if (description.Usage.HasFlag(TextureUsage.RenderTarget))
        {
            var rtvDesc = new RenderTargetViewDesc
            {
                Format = D3D11Formats.MapRtv(description.Format),
                ViewDimension = RtvDimension.Texture2D,
            };
            rtvDesc.Anonymous.Texture2D = new Tex2DRtv { MipSlice = 0 };

            ID3D11RenderTargetView* rtv = null;
            D3D11GraphicsDevice.Check(
                device->CreateRenderTargetView((ID3D11Resource*)Handle, &rtvDesc, ref rtv),
                "ID3D11Device::CreateRenderTargetView");
            RenderTargetView = rtv;
        }

        if (description.Usage.HasFlag(TextureUsage.DepthStencil))
        {
            var dsvDesc = new DepthStencilViewDesc
            {
                Format = D3D11Formats.MapDsv(description.Format),
                ViewDimension = DsvDimension.Texture2D,
                Flags = 0,
            };
            dsvDesc.Anonymous.Texture2D = new Tex2DDsv { MipSlice = 0 };

            ID3D11DepthStencilView* dsv = null;
            D3D11GraphicsDevice.Check(
                device->CreateDepthStencilView((ID3D11Resource*)Handle, &dsvDesc, ref dsv),
                "ID3D11Device::CreateDepthStencilView");
            DepthStencilView = dsv;
        }

        _ = context;
    }

    public void Dispose()
    {
        if (ShaderResourceView is not null) { ShaderResourceView->Release(); ShaderResourceView = null; }
        if (RenderTargetView is not null) { RenderTargetView->Release(); RenderTargetView = null; }
        if (DepthStencilView is not null) { DepthStencilView->Release(); DepthStencilView = null; }
        if (Handle is not null) { Handle->Release(); Handle = null; }
    }
}
