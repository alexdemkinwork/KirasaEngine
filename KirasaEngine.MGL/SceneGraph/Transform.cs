namespace KirasaEngine.MGL.SceneGraph;

public sealed class Transform
{
    public Vector3 LocalPosition { get; set; } = Vector3.Zero;
    public Quaternion LocalRotation { get; set; } = Quaternion.Identity;
    public Vector3 LocalScale { get; set; } = Vector3.One;

    internal Transform? Parent { get; set; }

    public Matrix4x4 LocalMatrix =>
        Matrix4x4.CreateScale(LocalScale) *
        Matrix4x4.CreateFromQuaternion(LocalRotation) *
        Matrix4x4.CreateTranslation(LocalPosition);

    /// <summary>
    /// Recomputed on every access by walking to the root. Scenes here are small (dozens/hundreds of nodes,
    /// evaluated once per node per frame during traversal) so this is deliberately simple over a
    /// dirty-flag cache that would need explicit propagation to children on every parent change.
    /// </summary>
    public Matrix4x4 WorldMatrix => Parent is null ? LocalMatrix : LocalMatrix * Parent.WorldMatrix;

    public Vector3 WorldPosition => WorldMatrix.Translation;

    public Vector3 Forward
    {
        get
        {
            Matrix4x4.Decompose(WorldMatrix, out _, out var rotation, out _);
            return Vector3.Normalize(Vector3.Transform(-Vector3.UnitZ, rotation));
        }
    }
}
