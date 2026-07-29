namespace KirasaEngine.MGL.SceneGraph;

public enum LightType
{
    Directional,
    Point,
}

public sealed class Light
{
    public LightType Type { get; set; } = LightType.Directional;
    public Vector3 Color { get; set; } = Vector3.One;
    public float Intensity { get; set; } = 1f;
}
