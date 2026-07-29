namespace KirasaEngine.MGL.Rendering.Backends.Vulkan;

internal sealed class VulkanResourceFactory(VulkanContext context) : IResourceFactory
{
    public IBuffer CreateBuffer(in BufferDescription description, ReadOnlySpan<byte> initialData = default) =>
        new VulkanBuffer(context, description, initialData);

    public ITexture CreateTexture(in TextureDescription description, ReadOnlySpan<byte> initialData = default) =>
        new VulkanTexture(context, description, initialData);

    public ISampler CreateSampler(in SamplerDescription description) => new VulkanSampler(context, description);

    public IShaderSet CreateShaderSet(ShaderSetDescription description) => new VulkanShaderSet(context, description);

    public IResourceLayout CreateResourceLayout(ResourceLayoutDescription description) => new VulkanResourceLayout(context, description);

    public IResourceSet CreateResourceSet(ResourceSetDescription description) => new VulkanResourceSet(context, description);

    public IPipeline CreatePipeline(PipelineDescription description) => new VulkanPipeline(context, description);

    public IRenderTarget CreateRenderTarget(in RenderTargetDescription description) => new VulkanRenderTarget(context, description);
}
