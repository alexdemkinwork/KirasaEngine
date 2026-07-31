using System;
using KirasaEngine.MGL.Rendering.RenderGraph.Passes;

namespace KirasaEngine.MGL.Rendering.RenderGraph;

/// <summary>
/// Manages the sequence of render passes and their dependencies.
/// </summary>
public class RenderGraph : IDisposable
{
    private readonly List<RenderPass> _passes = new();
    private readonly Dictionary<RenderGraphTextureUsage, IRenderTarget> _renderTargets = new();
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
    public void Execute(ICommandList cmd, RenderContext context)
    {
        // Resolve pass dependencies and allocate textures only for enabled passes
        AllocateTextures(context);
        
        cmd.Begin();
        
        // Execute passes in order
        foreach (var pass in _passes)
        {
            if (ShouldSkipPass(pass, context.Settings))
                continue;
            
            pass.Execute(cmd, context);
        }
        
        cmd.End();
        context.Device.Submit(cmd);
    }
    
    /// <summary>
    /// Allocates textures for all enabled passes in the graph.
    /// </summary>
    /// <param name="context">The render context.</param>
    private void AllocateTextures(RenderContext context)
    {
        foreach (var pass in _passes)
        {
            // Only allocate textures for passes that will actually run
            if (ShouldSkipPass(pass, context.Settings))
                continue;
                
            foreach (var output in pass.Outputs)
            {
                if (_renderTargets.ContainsKey(output))
                    continue;
                
                var textureDescription = GetTextureDescription(output, context.Width, context.Height, context);
                var renderTarget = _resourceManager.CreateRenderTarget($"{pass.Name}_{output}", textureDescription);
                _renderTargets[output] = renderTarget;
            }
        }
    }
    
    /// <summary>
    /// Gets the texture description for the given usage.
    /// </summary>
    /// <param name="usage">The texture usage.</param>
    /// <param name="width">The texture width.</param>
    /// <param name="height">The texture height.</param>
    /// <param name="context">The render context for settings.</param>
    /// <returns>The texture description.</returns>
    private TextureDescription GetTextureDescription(RenderGraphTextureUsage usage, uint width, uint height, RenderContext context)
    {
        return usage switch
        {
            RenderGraphTextureUsage.Color => new TextureDescription(width, height, TextureFormat.Rgba8UNorm, TextureUsage.RenderTarget),
            RenderGraphTextureUsage.Depth => new TextureDescription(width, height, TextureFormat.Rgba16Float, TextureUsage.RenderTarget),
            RenderGraphTextureUsage.Normal => new TextureDescription(width, height, TextureFormat.Rgba16Float, TextureUsage.RenderTarget),
            RenderGraphTextureUsage.ShadowMap => new TextureDescription(context.Settings.ShadowMapResolution, context.Settings.ShadowMapResolution, TextureFormat.R32Float, TextureUsage.RenderTarget),
            RenderGraphTextureUsage.AO => new TextureDescription(width, height, TextureFormat.R32Float, TextureUsage.RenderTarget),
            RenderGraphTextureUsage.HDR => new TextureDescription(width, height, TextureFormat.Rgba16Float, TextureUsage.RenderTarget),
            RenderGraphTextureUsage.Bloom => new TextureDescription(width, height, TextureFormat.Rgba16Float, TextureUsage.RenderTarget),
            RenderGraphTextureUsage.LDR => new TextureDescription(width, height, TextureFormat.Rgba8UNorm, TextureUsage.RenderTarget),
            RenderGraphTextureUsage.Final => new TextureDescription(width, height, TextureFormat.Rgba8UNorm, TextureUsage.RenderTarget),
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
            Passes.ShadowPass => !settings.ShadowsActive,
            Passes.Prepass => !settings.SSAOActive,
            Passes.SSAOPass => !settings.SSAOActive,
            Passes.BloomPass => !settings.BloomActive,
            Passes.CompositePass => !settings.FXAAActive && !settings.VignetteActive,
            Passes.FXAAPass => !settings.FXAAActive,
            _ => false,
        };
    }
    
    /// <summary>
    /// Gets the render target for the given usage.
    /// </summary>
    /// <param name="usage">The texture usage.</param>
    /// <returns>The render target.</returns>
    public IRenderTarget GetRenderTarget(RenderGraphTextureUsage usage)
    {
        return _renderTargets[usage];
    }
    
    /// <summary>
    /// Gets the texture for the given usage.
    /// </summary>
    /// <param name="usage">The texture usage.</param>
    /// <returns>The texture.</returns>
    public ITexture GetTexture(RenderGraphTextureUsage usage)
    {
        return _renderTargets[usage].ColorTexture;
    }
    
    /// <summary>
    /// Disposes all managed resources.
    /// </summary>
    public void Dispose()
    {
        _resourceManager.Dispose();
    }
}
