using Silk.NET.OpenGL;

namespace KirasaEngine.MGL.Rendering.Backends.OpenGL;

internal sealed class GLResourceFactory(GL gl) : IResourceFactory
{
    public IBuffer CreateBuffer(in BufferDescription description, ReadOnlySpan<byte> initialData = default) =>
        new GLBuffer(gl, description, initialData);

    public ITexture CreateTexture(in TextureDescription description, ReadOnlySpan<byte> initialData = default) =>
        new GLTexture(gl, description, initialData);

    public ISampler CreateSampler(in SamplerDescription description) => new GLSampler(gl, description);

    public IShaderSet CreateShaderSet(ShaderSetDescription description) => new GLShaderSet(gl, description);

    public IResourceLayout CreateResourceLayout(ResourceLayoutDescription description) => new GLResourceLayout(description);

    public IResourceSet CreateResourceSet(ResourceSetDescription description) => new GLResourceSet(description);

    public IPipeline CreatePipeline(PipelineDescription description) => new GLPipeline(gl, description);

    public IRenderTarget CreateRenderTarget(in RenderTargetDescription description) => new GLRenderTarget(gl, description);
}
