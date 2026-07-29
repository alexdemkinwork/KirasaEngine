namespace KirasaEngine.MGL.Rendering.Abstractions;

public interface IResourceFactory
{
    IBuffer CreateBuffer(in BufferDescription description, ReadOnlySpan<byte> initialData = default);
    ITexture CreateTexture(in TextureDescription description, ReadOnlySpan<byte> initialData = default);
    ISampler CreateSampler(in SamplerDescription description);
    IShaderSet CreateShaderSet(ShaderSetDescription description);
    IResourceLayout CreateResourceLayout(ResourceLayoutDescription description);
    IResourceSet CreateResourceSet(ResourceSetDescription description);
    IPipeline CreatePipeline(PipelineDescription description);
    IRenderTarget CreateRenderTarget(in RenderTargetDescription description);
}
