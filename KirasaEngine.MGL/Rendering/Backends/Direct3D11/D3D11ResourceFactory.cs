using Silk.NET.Direct3D11;

using KirasaEngine.MGL.Rendering;

namespace KirasaEngine.MGL.Rendering.Backends.Direct3D11;

/// <summary>
/// Thin constructor forwarder, exactly like <c>GLResourceFactory</c>: it just hands the device (and, for the
/// resources that need to upload through it, the immediate context) to each concrete resource type.
/// </summary>
internal sealed unsafe class D3D11ResourceFactory : IResourceFactory
{
    private readonly ID3D11Device* _device;
    private readonly ID3D11DeviceContext* _context;

    public D3D11ResourceFactory(ID3D11Device* device, ID3D11DeviceContext* context)
    {
        _device = device;
        _context = context;
    }

    public IBuffer CreateBuffer(in BufferDescription description, ReadOnlySpan<byte> initialData = default) =>
        new D3D11Buffer(_device, _context, description, initialData);

    public ITexture CreateTexture(in TextureDescription description, ReadOnlySpan<byte> initialData = default) =>
        new D3D11Texture(_device, _context, description, initialData);

    public ISampler CreateSampler(in SamplerDescription description) => new D3D11Sampler(_device, description);

    public IShaderSet CreateShaderSet(ShaderSetDescription description) => new D3D11ShaderSet(_device, description);

    public IResourceLayout CreateResourceLayout(ResourceLayoutDescription description) => new D3D11ResourceLayout(description);

    public IResourceSet CreateResourceSet(ResourceSetDescription description) => new D3D11ResourceSet(description);

    public IPipeline CreatePipeline(PipelineDescription description) => new D3D11Pipeline(_device, description);

    public IRenderTarget CreateRenderTarget(in RenderTargetDescription description) =>
        new D3D11RenderTarget(_device, _context, description);
}
