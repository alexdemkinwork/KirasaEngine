namespace KirasaEngine.MGL.Rendering.Abstractions.Descriptions;

/// <summary>Binds concrete resources (in <see cref="IResourceLayout"/> element order) to a layout.</summary>
public sealed class ResourceSetDescription
{
    public required IResourceLayout Layout { get; init; }
    public required object[] Resources { get; init; }
}
