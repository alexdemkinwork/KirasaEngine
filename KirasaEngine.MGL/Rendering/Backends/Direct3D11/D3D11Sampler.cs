using Silk.NET.Direct3D11;

using KirasaEngine.MGL.Rendering;

namespace KirasaEngine.MGL.Rendering.Backends.Direct3D11;

internal sealed unsafe class D3D11Sampler : ISampler
{
    public ID3D11SamplerState* Handle;

    public D3D11Sampler(ID3D11Device* device, in SamplerDescription description)
    {
        var address = D3D11Formats.MapAddressMode(description.AddressMode);

        var desc = new SamplerDesc
        {
            Filter = D3D11Formats.MapFilter(description.Filter),
            AddressU = address,
            AddressV = address,
            AddressW = address,
            MipLODBias = 0f,
            MaxAnisotropy = 1,
            ComparisonFunc = ComparisonFunc.Never,
            MinLOD = 0f,
            MaxLOD = float.MaxValue,
        };
        desc.BorderColor[0] = 0f;
        desc.BorderColor[1] = 0f;
        desc.BorderColor[2] = 0f;
        desc.BorderColor[3] = 0f;

        ID3D11SamplerState* handle = null;
        D3D11GraphicsDevice.Check(device->CreateSamplerState(&desc, ref handle), "ID3D11Device::CreateSamplerState");
        Handle = handle;
    }

    public void Dispose()
    {
        if (Handle is null) return;
        Handle->Release();
        Handle = null;
    }
}
