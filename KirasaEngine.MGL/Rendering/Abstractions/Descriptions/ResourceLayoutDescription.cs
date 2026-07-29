namespace KirasaEngine.MGL.Rendering.Abstractions.Descriptions;

public readonly struct ResourceLayoutElementDescription(string name, ResourceKind kind, ShaderStage stages, uint binding)
{
    public readonly string Name = name;
    public readonly ResourceKind Kind = kind;
    public readonly ShaderStage Stages = stages;
    public readonly uint Binding = binding;
}

public sealed class ResourceLayoutDescription
{
    public required ResourceLayoutElementDescription[] Elements { get; init; }
}
