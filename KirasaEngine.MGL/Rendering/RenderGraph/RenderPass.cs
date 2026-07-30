namespace KirasaEngine.MGL.Rendering.RenderGraph;

/// <summary>
/// Base class for all render passes in the render graph.
/// </summary>
public abstract class RenderPass
{
    /// <summary>
    /// Gets the name of the pass.
    /// </summary>
    public string Name { get; }
    
    /// <summary>
    /// Gets the list of input textures required by this pass.
    /// </summary>
    public IReadOnlyList<TextureUsage> Inputs { get; }
    
    /// <summary>
    /// Gets the list of output textures produced by this pass.
    /// </summary>
    public IReadOnlyList<TextureUsage> Outputs { get; }
    
    /// <summary>
    /// Initializes a new instance of the <see cref="RenderPass"/> class.
    /// </summary>
    /// <param name="name">The name of the pass.</param>
    /// <param name="inputs">The input textures required by this pass.</param>
    /// <param name="outputs">The output textures produced by this pass.</param>
    protected RenderPass(string name, IReadOnlyList<TextureUsage> inputs, IReadOnlyList<TextureUsage> outputs)
    {
        Name = name;
        Inputs = inputs;
        Outputs = outputs;
    }
    
    /// <summary>
    /// Executes the pass.
    /// </summary>
    /// <param name="cmd">The command list to record commands into.</param>
    /// <param name="context">The render context providing access to resources and scene data.</param>
    public abstract void Execute(IGraphicsCommandList cmd, RenderContext context);
}