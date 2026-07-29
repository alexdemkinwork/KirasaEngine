using Silk.NET.Direct3D12;

namespace KirasaEngine.MGL.Rendering.Backends.Direct3D12;

/// <summary>
/// D3D12 has no free-standing sampler object to bind (D3D11's <c>ID3D11SamplerState</c> + <c>PSSetSamplers</c>
/// has no equivalent): a sampler is a descriptor written into a shader-visible SAMPLER heap and reached
/// through a descriptor table. So this type is really just "the heap slot my SAMPLER_DESC lives in".
///
/// <para>The plan floated baking a <i>static</i> sampler into the root signature instead, since this project
/// only ever creates LinearWrap. A real sampler heap is barely more code and keeps
/// <see cref="SamplerDescription"/> meaningful — a PointClamp sampler actually behaves like one here rather
/// than being silently ignored — so that's the route taken.</para>
/// </summary>
internal sealed unsafe class D3D12Sampler : ISampler
{
    /// <summary>Handle into the device's shader-visible sampler heap, bound as a one-entry descriptor table.</summary>
    public GpuDescriptorHandle GpuHandle { get; }

    public D3D12Sampler(D3D12GraphicsDevice device, in SamplerDescription description)
    {
        var address = D3D12Formats.MapAddressMode(description.AddressMode);

        var desc = new SamplerDesc
        {
            Filter = D3D12Formats.MapFilter(description.Filter),
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

        var index = device.SamplerHeap.Allocate();
        device.NativeDevice->CreateSampler(&desc, device.SamplerHeap.Cpu(index));
        GpuHandle = device.SamplerHeap.Gpu(index);
    }

    /// <summary>Nothing to release: the descriptor's slot belongs to the device's heap (see D3D12DescriptorAllocator).</summary>
    public void Dispose() { }
}
