namespace KirasaEngine.MGL.Rendering.Abstractions.Descriptions;

/// <summary>
/// Names a logical shader (resolved against <see cref="Rendering.ShaderLibrary.ShaderLibrary"/> for the active
/// backend's dialect) and the vertex buffer slots it expects.
/// </summary>
public sealed class ShaderSetDescription
{
    public required string ShaderName { get; init; }
    public required VertexLayoutDescription[] VertexLayouts { get; init; }
}
