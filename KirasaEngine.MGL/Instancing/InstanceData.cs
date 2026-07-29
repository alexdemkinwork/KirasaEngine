namespace KirasaEngine.MGL.Instancing;

/// <summary>Per-instance GPU data: world matrix (row-major, matches <see cref="System.Numerics.Matrix4x4"/>) + a color tint.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct InstanceData(Matrix4x4 world, Vector4 colorTint)
{
    public Matrix4x4 World = world;
    public Vector4 ColorTint = colorTint;

    public const uint SizeInBytes = 80;

    public static InstanceData Identity => new(Matrix4x4.Identity, Vector4.One);

    /// <summary>Four consecutive Float4 locations carry the matrix rows/columns; <paramref name="baseLocation"/> is the first free shader input location.</summary>
    public static VertexLayoutDescription GetVertexLayout(uint baseLocation) => new(
        SizeInBytes,
        VertexInputRate.PerInstance,
        new VertexElementDescription("InstanceWorld0", baseLocation + 0, VertexElementFormat.Float4, 0),
        new VertexElementDescription("InstanceWorld1", baseLocation + 1, VertexElementFormat.Float4, 16),
        new VertexElementDescription("InstanceWorld2", baseLocation + 2, VertexElementFormat.Float4, 32),
        new VertexElementDescription("InstanceWorld3", baseLocation + 3, VertexElementFormat.Float4, 48),
        new VertexElementDescription("InstanceColor", baseLocation + 4, VertexElementFormat.Float4, 64));
}
