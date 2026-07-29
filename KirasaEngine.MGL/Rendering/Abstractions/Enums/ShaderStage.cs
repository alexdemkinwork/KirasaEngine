namespace KirasaEngine.MGL.Rendering.Abstractions.Enums;

[Flags]
public enum ShaderStage
{
    Vertex = 1 << 0,
    Fragment = 1 << 1,
}
