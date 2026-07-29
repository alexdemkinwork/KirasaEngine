namespace KirasaEngine.MGL.Rendering.Abstractions;

public interface IBuffer : IDisposable
{
    uint SizeInBytes { get; }
    BufferUsage Usage { get; }
}
