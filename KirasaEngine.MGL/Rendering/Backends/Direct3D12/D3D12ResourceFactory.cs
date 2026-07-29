namespace KirasaEngine.MGL.Rendering.Backends.Direct3D12;

/// <summary>
/// Threads the owning device (and through it the ID3D12Device, the shared descriptor heaps and the blocking
/// upload command list) into every resource type. Mirrors <c>GLResourceFactory</c>'s shape.
/// </summary>
internal sealed class D3D12ResourceFactory(D3D12GraphicsDevice device) : IResourceFactory
{
    public IBuffer CreateBuffer(in BufferDescription description, ReadOnlySpan<byte> initialData = default) =>
        new D3D12Buffer(device, description, initialData);

    public ITexture CreateTexture(in TextureDescription description, ReadOnlySpan<byte> initialData = default) =>
        new D3D12Texture(device, description, initialData);

    public ISampler CreateSampler(in SamplerDescription description) => new D3D12Sampler(device, description);

    public IShaderSet CreateShaderSet(ShaderSetDescription description) => new D3D12ShaderSet(description);

    public IResourceLayout CreateResourceLayout(ResourceLayoutDescription description) =>
        new D3D12ResourceLayout(device, description);

    public IResourceSet CreateResourceSet(ResourceSetDescription description) => new D3D12ResourceSet(description);

    public IPipeline CreatePipeline(PipelineDescription description) => new D3D12Pipeline(device, description);

    public IRenderTarget CreateRenderTarget(in RenderTargetDescription description) =>
        new D3D12RenderTarget(device, description);
}
