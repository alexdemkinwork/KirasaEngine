namespace KirasaEngine.MGL.Rendering;

/// <summary>Minimal single-slot vertex format for full-screen passes (SSAO, blur, composite, FXAA): clip-space position + UV, no instancing.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct PostProcessVertex(Vector2 position, Vector2 uv)
{
    public Vector2 Position = position;
    public Vector2 UV = uv;

    public const uint SizeInBytes = 16;

    public static VertexLayoutDescription GetVertexLayout() => new(
        SizeInBytes,
        VertexInputRate.PerVertex,
        new VertexElementDescription("Position", 0, VertexElementFormat.Float2, 0),
        new VertexElementDescription("TEXCOORD", 1, VertexElementFormat.Float2, 8));

    /// <summary>The classic "big triangle" full-screen trick: one triangle whose clip-space extent covers the whole viewport, avoiding the diagonal seam a two-triangle quad would need.</summary>
    public static PostProcessVertex[] FullscreenTriangleVertices { get; } =
    [
        new(new Vector2(-1, -1), new Vector2(0, 0)),
        new(new Vector2(3, -1), new Vector2(2, 0)),
        new(new Vector2(-1, 3), new Vector2(0, 2)),
    ];

    public static uint[] FullscreenTriangleIndices { get; } = [0, 1, 2];
}
