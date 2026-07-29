namespace KirasaEngine.MGL.Rendering.Abstractions.Structs;

/// <summary>Describes one attribute within a vertex/instance buffer layout, addressed by shader input <see cref="Location"/>.</summary>
public readonly struct VertexElementDescription(string name, uint location, VertexElementFormat format, uint offset)
{
    public readonly string Name = name;
    public readonly uint Location = location;
    public readonly VertexElementFormat Format = format;
    public readonly uint Offset = offset;

    public static uint SizeOf(VertexElementFormat format) => format switch
    {
        VertexElementFormat.Float1 => 4,
        VertexElementFormat.Float2 => 8,
        VertexElementFormat.Float3 => 12,
        VertexElementFormat.Float4 => 16,
        _ => throw new ArgumentOutOfRangeException(nameof(format)),
    };
}
