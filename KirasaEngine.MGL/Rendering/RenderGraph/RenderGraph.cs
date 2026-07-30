using System;

namespace KirasaEngine.MGL.Rendering.RenderGraph;

/// <summary>
/// Manages the sequence of render passes and their dependencies.
/// </summary>
public class RenderGraph : IDisposable
{
    private readonly List<RenderPass> _passes = new();
    private readonly Dictionary<TextureUsage, ITexture> _textures = new();
    private readonly ResourceManager _resourceManager;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="RenderGraph"/> class.
    /// </summary>
    /// <param name="device">The graphics device.</param>
    public RenderGraph(IGraphicsDevice device)
    {
        _resourceManager = new ResourceManager(device);
    }
    
    /// <summary>
    /// Adds a pass to the render graph.
    /// </summary>
    /// <param name="pass">The pass to add.</param>
    public void AddPass(RenderPass pass)
    {
        _passes.Add(pass);
    }
    
    /// <summary>
    /// Executes the render graph.
    /// </summary>
    /// <param name="cmd">The command list to record commands into.</param>
    /// <param name="context">The render context.</param>
    public void Execute(IGraphicsCommandList cmd, RenderContext context)
    {
        // Resolve pass dependencies and allocate textures
        AllocateTextures(context);
        
        // Execute passes in order
        foreach (var pass in _passes)
        {
            if (ShouldSkipPass(pass, context.Settings))
                continue;
            
            pass.Execute(cmd, context);
        }
    }
    
    /// <summary>
    /// Allocates textures for all passes in the graph.
    /// </summary>
    /// <param name="context">The render context.</param>
    private void AllocateTextures(RenderContext context)
    {
        foreach (var pass in _passes)
        {
            foreach (var output in pass.Outputs)
            {
                if (_textures.ContainsKey(output))
                    continue;
                
                var description = GetTextureDescription(output, context.Width, context.Height);
                var texture = _resourceManager.GetOrCreateTexture($"{pass.Name}_{output}", description);
                _textures[output] = texture;
            }
        }
    }
    
    /// <summary>
    /// Gets the texture description for the given usage.
    /// </summary>
    /// <param name="usage">The texture usage.</param>
    /// <param name="width">The texture width.</param>
    /// <param name="height">The texture height.</param>
    /// <returns>The texture description.</returns>
    private TextureDescription GetTextureDescription(TextureUsage usage, uint width, uint height)
    {
        return usage switch
        {
            TextureUsage.Color => new TextureDescription(width, height, TextureFormat.Rgba8UNorm, TextureUsage.RenderTarget),
            TextureUsage.Depth => new TextureDescription(width, height, TextureFormat.Depth24Stencil8, TextureUsage.RenderTarget),
            TextureUsage.Normal => new TextureDescription(width, height, TextureFormat.Rgba16Float, TextureUsage.RenderTarget),
            TextureUsage.ShadowMap => new TextureDescription(context.Settings.ShadowMapResolution, context.Settings.ShadowMapResolution, TextureFormat.R32Float, TextureUsage.RenderTarget),
            TextureUsage.AO => new TextureDescription(width, height, TextureFormat.R32Float, TextureUsage.RenderTarget),
            TextureUsage.HDR => new TextureDescription(width, height, TextureFormat.Rgba16Float, TextureUsage.RenderTarget),
            TextureUsage.Bloom => new TextureDescription(width, height, TextureFormat.Rgba16Float, TextureUsage.RenderTarget),
            TextureUsage.LDR => new TextureDescription(width, height, TextureFormat.Rgba8UNorm, TextureUsage.RenderTarget),
            TextureUsage.Final => new TextureDescription(width, height, TextureFormat.Rgba8UNorm, TextureUsage.RenderTarget),
            _ => throw new ArgumentOutOfRangeException(nameof(usage), usage, null),
        };
    }
    
    /// <summary>
    /// Determines whether the pass should be skipped based on the post-process settings.
    /// </summary>
    /// <param name="pass">The pass to check.</param>
    /// <param name="settings">The post-process settings.</param>
    /// <returns><c>true</c> if the pass should be skipped; otherwise, <c>false</c>.</returns>
    private bool ShouldSkipPass(RenderPass pass, PostProcessSettings settings)
    {
        return pass switch
        {
            ShadowPass => !settings.ShadowsActive,
            SSAOPass => !settings.SSAOActive,
            BloomPass => !settings.BloomActive,
            FXAAPass => !settings.FXAAActive,
            _ => false,
        };
    }
    
    /// <summary>
    /// Gets the texture for the given usage.
    /// </summary>
    /// <param name="usage">The texture usage.</param>
    /// <returns>The texture.</returns>
    public ITexture GetTexture(TextureUsage usage)
    {
        return _textures[usage];
    }
    
    /// <summary>
    /// Disposes all managed resources.
    /// </summary>
    public void Dispose()
    {
        _resourceManager.Dispose();
    }
}