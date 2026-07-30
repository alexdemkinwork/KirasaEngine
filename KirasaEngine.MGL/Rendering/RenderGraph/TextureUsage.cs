namespace KirasaEngine.MGL.Rendering.RenderGraph;

/// <summary>
/// Defines the usage of a texture in the render graph.
/// </summary>
public enum RenderGraphTextureUsage
{
    /// <summary>Color output from the forward pass.</summary>
    Color,
    
    /// <summary>Depth buffer.</summary>
    Depth,
    
    /// <summary>View-space normals (for SSAO).</summary>
    Normal,
    
    /// <summary>Shadow map (depth from light's perspective).</summary>
    ShadowMap,
    
    /// <summary>Ambient occlusion term.</summary>
    AO,
    
    /// <summary>High-dynamic-range scene color (before tonemapping).</summary>
    HDR,
    
    /// <summary>Bloom texture (blurred bright areas).</summary>
    Bloom,
    
    /// <summary>Low-dynamic-range scene color (after tonemapping).</summary>
    LDR,
    
    /// <summary>Final output (after post-processing).</summary>
    Final,
}