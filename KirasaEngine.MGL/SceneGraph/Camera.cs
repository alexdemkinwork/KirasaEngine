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

    public Matrix4x4 GetProjectionMatrix(float aspectRatio)
    {
        if (Orthographic)
        {
            var orthoProj = Matrix4x4.CreateOrthographic(OrthographicSize * aspectRatio, OrthographicSize, NearPlane, FarPlane);
            // Convert from default [-1,1] depth range to [0,1]
            orthoProj.M33 = 2f / (FarPlane - NearPlane);
            orthoProj.M34 = 0;
            orthoProj.M43 = -(FarPlane + NearPlane) / (FarPlane - NearPlane);
            orthoProj.M44 = 1;
            return orthoProj;
        }
        
        var fov = FieldOfViewRadians;
        var tanHalfFov = MathF.Tan(fov * 0.5f);
        
        // Create perspective matrix with depth range [0,1] (DirectX/Vulkan convention)
        // This matches what Camera's doc comment promises.
        return new Matrix4x4
        {
            M11 = 1f / (aspectRatio * tanHalfFov),
            M22 = 1f / tanHalfFov,
            M33 = FarPlane / (FarPlane - NearPlane),
            M34 = 1f,
            M43 = -(FarPlane * NearPlane) / (FarPlane - NearPlane),
            M44 = 0f
        };
    }
}
