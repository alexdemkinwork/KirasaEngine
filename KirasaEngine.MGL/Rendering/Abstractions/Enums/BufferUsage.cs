namespace KirasaEngine.MGL.Rendering.Abstractions.Enums;

[Flags]
public enum BufferUsage
{
    Vertex = 1 << 0,
    Index = 1 << 1,
    Uniform = 1 << 2,
    Structured = 1 << 3,

    /// <summary>Content is expected to change frequently via <see cref="ICommandList.UpdateBuffer"/>.</summary>
    Dynamic = 1 << 4,

    /// <summary>Buffer can be mapped and read back on the CPU (used internally for render-target readback).</summary>
    StagingRead = 1 << 5,
}
