namespace KirasaEngine.MGL.Rendering.Abstractions.Descriptions;

public readonly struct BufferDescription(uint sizeInBytes, BufferUsage usage)
{
    public readonly uint SizeInBytes = sizeInBytes;
    public readonly BufferUsage Usage = usage;
}
