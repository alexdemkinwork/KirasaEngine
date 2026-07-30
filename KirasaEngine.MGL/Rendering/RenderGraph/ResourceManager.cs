using System;

using KirasaEngine.MGL.Rendering;

namespace KirasaEngine.MGL.Rendering.RenderGraph;

/// <summary>
/// Manages GPU resources (textures, buffers, samplers) with lifetime tracking.
/// </summary>
public class ResourceManager : IDisposable
{
    private readonly IGraphicsDevice _device;
    private readonly Dictionary<string, ITexture> _textureCache = new();
    private readonly Dictionary<string, IBuffer> _bufferCache = new();
    private readonly Dictionary<string, ISampler> _samplerCache = new();
    private readonly Dictionary<string, IPipeline> _pipelineCache = new();
    private readonly Dictionary<string, IResourceLayout> _resourceLayoutCache = new();
    private readonly List<IDisposable> _disposables = new();
    
    /// <summary>
    /// Initializes a new instance of the <see cref="ResourceManager"/> class.
    /// </summary>
    /// <param name="device">The graphics device.</param>
    public ResourceManager(IGraphicsDevice device)
    {
        _device = device;
    }
    
    /// <summary>
    /// Gets or creates a texture.
    /// </summary>
    /// <param name="key">The unique key for the texture.</param>
    /// <param name="description">The texture description.</param>
    /// <param name="data">Optional initial data for the texture.</param>
    /// <returns>The texture.</returns>
    public ITexture GetOrCreateTexture(string key, TextureDescription description, ReadOnlySpan<byte> data = default)
    {
        if (_textureCache.TryGetValue(key, out var texture))
            return texture;
        
        texture = _device.Factory.CreateTexture(description, data);
        _textureCache[key] = texture;
        _disposables.Add(texture);
        return texture;
    }
    
    /// <summary>
    /// Gets or creates a buffer.
    /// </summary>
    /// <param name="key">The unique key for the buffer.</param>
    /// <param name="description">The buffer description.</param>
    /// <param name="data">Optional initial data for the buffer.</param>
    /// <returns>The buffer.</returns>
    public IBuffer GetOrCreateBuffer(string key, BufferDescription description, ReadOnlySpan<byte> data = default)
    {
        if (_bufferCache.TryGetValue(key, out var buffer))
            return buffer;
        
        buffer = _device.Factory.CreateBuffer(description, data);
        _bufferCache[key] = buffer;
        _disposables.Add(buffer);
        return buffer;
    }
    
    /// <summary>
    /// Gets or creates a sampler.
    /// </summary>
    /// <param name="key">The unique key for the sampler.</param>
    /// <param name="description">The sampler description.</param>
    /// <returns>The sampler.</returns>
    public ISampler GetOrCreateSampler(string key, SamplerDescription description)
    {
        if (_samplerCache.TryGetValue(key, out var sampler))
            return sampler;
        
        sampler = _device.Factory.CreateSampler(description);
        _samplerCache[key] = sampler;
        _disposables.Add(sampler);
        return sampler;
    }
    
    /// <summary>
    /// Gets or creates a pipeline.
    /// </summary>
    /// <param name="key">The unique key for the pipeline.</param>
    /// <param name="description">The pipeline description.</param>
    /// <returns>The pipeline.</returns>
    public IPipeline GetOrCreatePipeline(string key, PipelineDescription description)
    {
        if (_pipelineCache.TryGetValue(key, out var pipeline))
            return pipeline;
        
        pipeline = _device.Factory.CreatePipeline(description);
        _pipelineCache[key] = pipeline;
        _disposables.Add(pipeline);
        return pipeline;
    }
    
    /// <summary>
    /// Gets or creates a resource layout.
    /// </summary>
    /// <param name="key">The unique key for the resource layout.</param>
    /// <param name="description">The resource layout description.</param>
    /// <returns>The resource layout.</returns>
    public IResourceLayout GetOrCreateResourceLayout(string key, ResourceLayoutDescription description)
    {
        if (_resourceLayoutCache.TryGetValue(key, out var layout))
            return layout;
        
        layout = _device.Factory.CreateResourceLayout(description);
        _resourceLayoutCache[key] = layout;
        _disposables.Add(layout);
        return layout;
    }
    
    /// <summary>
    /// Uploads data to a buffer.
    /// </summary>
    /// <param name="cmd">The command list to record commands into.</param>
    /// <param name="buffer">The buffer to update.</param>
    /// <param name="data">The data to upload.</param>
    public void UploadBufferData(ICommandList cmd, IBuffer buffer, ReadOnlySpan<byte> data)
    {
        cmd.UpdateBuffer(buffer, data);
    }
    
    /// <summary>
    /// Allocates a descriptor set for Vulkan/D3D12.
    /// </summary>
    /// <param name="layout">The resource layout.</param>
    /// <param name="resources">The resources to bind.</param>
    /// <returns>The descriptor set.</returns>
    public IResourceSet AllocateDescriptorSet(IResourceLayout layout, IReadOnlyList<object> resources)
    {
        var set = _device.Factory.CreateResourceSet(new ResourceSetDescription { Layout = layout, Resources = resources });
        _disposables.Add(set);
        return set;
    }
    
    /// <summary>
    /// Disposes all managed resources.
    /// </summary>
    public void Dispose()
    {
        foreach (var disposable in _disposables)
            disposable.Dispose();
        
        _textureCache.Clear();
        _bufferCache.Clear();
        _samplerCache.Clear();
        _pipelineCache.Clear();
        _resourceLayoutCache.Clear();
        _disposables.Clear();
    }
}