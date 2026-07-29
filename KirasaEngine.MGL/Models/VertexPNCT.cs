namespace KirasaEngine.MGL.Models;

/// <summary>Standard mesh vertex: position, normal, vertex color (used as a tint) and a single UV set.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct VertexPNCT(Vector3 position, Vector3 normal, Vector4 color, Vector2 uv)
{
    public Vector3 Position = position;
    public Vector3 Normal = normal;
    public Vector4 Color = color;
    public Vector2 UV = uv;

    public const uint SizeInBytes = 48;

    public static VertexLayoutDescription GetVertexLayout() => new(
        SizeInBytes,
        VertexInputRate.PerVertex,
        new VertexElementDescription("Position", 0, VertexElementFormat.Float3, 0),
        new VertexElementDescription("Normal", 1, VertexElementFormat.Float3, 12),
        new VertexElementDescription("Color", 2, VertexElementFormat.Float4, 24),
        new VertexElementDescription("TEXCOORD", 3, VertexElementFormat.Float2, 40));
}
