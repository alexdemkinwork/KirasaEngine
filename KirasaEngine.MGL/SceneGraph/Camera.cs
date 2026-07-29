namespace KirasaEngine.MGL.SceneGraph;

/// <summary>
/// Produces System.Numerics-convention matrices: Y-up, depth range [0,1] (D3D/Vulkan native), and, like
/// <see cref="Transform.Forward"/>, local -Z as the look direction (verified empirically: a point at local
/// (0,0,-5) comes out with positive clip-space W; (0,0,+5) comes out negative, i.e. behind the camera).
/// The OpenGL backend is responsible for reconciling its native [-1,1] clip-space depth (e.g. via
/// glClipControl(LOWER_LEFT, ZERO_TO_ONE)) and the Vulkan backend for its flipped NDC Y-axis
/// (e.g. via a negative-height viewport) - Camera itself stays backend-agnostic.
/// </summary>
public sealed class Camera
{
    public float FieldOfViewRadians { get; set; } = MathF.PI / 4f;
    public float NearPlane { get; set; } = 0.05f;
    public float FarPlane { get; set; } = 1000f;
    public bool Orthographic { get; set; }
    public float OrthographicSize { get; set; } = 5f;

    public Matrix4x4 GetViewMatrix(Transform transform)
    {
        Matrix4x4.Invert(transform.WorldMatrix, out var view);
        return view;
    }

    public Matrix4x4 GetProjectionMatrix(float aspectRatio) => Orthographic
        ? Matrix4x4.CreateOrthographic(OrthographicSize * aspectRatio, OrthographicSize, NearPlane, FarPlane)
        : Matrix4x4.CreatePerspectiveFieldOfView(FieldOfViewRadians, MathF.Max(aspectRatio, 0.0001f), NearPlane, FarPlane);
}
