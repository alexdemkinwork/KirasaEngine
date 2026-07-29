using Silk.NET.OpenGL;

namespace KirasaEngine.MGL.Rendering.Backends.OpenGL;

internal sealed class GLBuffer : IBuffer
{
    private readonly GL _gl;

    public uint Handle { get; }
    public BufferTargetARB Target { get; }
    public uint SizeInBytes { get; }
    public BufferUsage Usage { get; }

    public unsafe GLBuffer(GL gl, in BufferDescription description, ReadOnlySpan<byte> initialData)
    {
        _gl = gl;
        SizeInBytes = description.SizeInBytes;
        Usage = description.Usage;
        Target = InferTarget(description.Usage);

        Handle = _gl.GenBuffer();
        _gl.BindBuffer(Target, Handle);

        var hint = description.Usage.HasFlag(BufferUsage.Dynamic) ? BufferUsageARB.DynamicDraw : BufferUsageARB.StaticDraw;

        if (!initialData.IsEmpty)
        {
            fixed (byte* ptr = initialData)
                _gl.BufferData(Target, (nuint)initialData.Length, ptr, hint);
        }
        else
        {
            _gl.BufferData(Target, description.SizeInBytes, null, hint);
        }

        _gl.BindBuffer(Target, 0);
    }

    public unsafe void SetData(ReadOnlySpan<byte> data, uint destinationOffsetBytes)
    {
        _gl.BindBuffer(Target, Handle);
        fixed (byte* ptr = data)
            _gl.BufferSubData(Target, (nint)destinationOffsetBytes, (nuint)data.Length, ptr);
        _gl.BindBuffer(Target, 0);
    }

    private static BufferTargetARB InferTarget(BufferUsage usage)
    {
        if (usage.HasFlag(BufferUsage.Index)) return BufferTargetARB.ElementArrayBuffer;
        if (usage.HasFlag(BufferUsage.Uniform)) return BufferTargetARB.UniformBuffer;
        if (usage.HasFlag(BufferUsage.Structured)) return BufferTargetARB.ShaderStorageBuffer;
        return BufferTargetARB.ArrayBuffer;
    }

    public void Dispose() => _gl.DeleteBuffer(Handle);
}
