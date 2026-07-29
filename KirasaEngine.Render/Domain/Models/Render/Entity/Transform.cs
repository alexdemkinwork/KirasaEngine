namespace KirasaEngine.Render.Domain.Models.Render.Entity;

[StructLayout(LayoutKind.Sequential)]
public struct Transform
{
    /// <summary>
    /// Позиция
    /// </summary>
    public Vector3 Position { get; set; }

    /// <summary>
    /// Вращение
    /// </summary>
    public Quaternion Rotation { get; set; }

    /// <summary>
    /// Масштаб
    /// </summary>
    public Vector3 Scale { get; set; }
}
