namespace KirasaEngine.Render.Domain.Models.Render.Entity;

[StructLayout(LayoutKind.Sequential)]
public struct Mesh
{
    /// <summary>
    /// Вершины
    /// </summary>
    public float[] Vertices { get; set; }
    /// <summary>
    /// Индексы
    /// </summary>
    public int[] Indices { get; set; }
}
