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
        GLErrorChecker.ValidateHandle(Handle, "Buffer");
        GLErrorChecker.CheckError(_gl, "GenBuffer");
        
        _gl.BindBuffer(Target, Handle);
        GLErrorChecker.CheckError(_gl, "BindBuffer");

        var hint = description.Usage.HasFlag(BufferUsage.Dynamic) ? BufferUsageARB.DynamicDraw : BufferUsageARB.StaticDraw;

        if (!initialData.IsEmpty)
        {
            fixed (byte* ptr = initialData)
                _gl.BufferData(Target, (nuint)initialData.Length, ptr, hint);
            GLErrorChecker.CheckError(_gl, "BufferData with initial data");
        }
        else
        {
            _gl.BufferData(Target, description.SizeInBytes, null, hint);
            GLErrorChecker.CheckError(_gl, "BufferData without initial data");
        }

        _gl.BindBuffer(Target, 0);
        GLErrorChecker.CheckError(_gl, "BindBuffer reset");
    }

    public unsafe void SetData(ReadOnlySpan<byte> data, uint destinationOffsetBytes)
    {
        GLErrorChecker.ValidateHandle(Handle, "Buffer");
        _gl.BindBuffer(Target, Handle);
        GLErrorChecker.CheckError(_gl, "BindBuffer for SetData");
        fixed (byte* ptr = data)
            _gl.BufferSubData(Target, (nint)destinationOffsetBytes, (nuint)data.Length, ptr);
        GLErrorChecker.CheckError(_gl, "BufferSubData");
        _gl.BindBuffer(Target, 0);
        GLErrorChecker.CheckError(_gl, "BindBuffer reset after SetData");
    }

    private static BufferTargetARB InferTarget(BufferUsage usage)
    {
        if (usage.HasFlag(BufferUsage.Index)) return BufferTargetARB.ElementArrayBuffer;
        if (usage.HasFlag(BufferUsage.Uniform)) return BufferTargetARB.UniformBuffer;
        if (usage.HasFlag(BufferUsage.Structured)) return BufferTargetARB.ShaderStorageBuffer;
        return BufferTargetARB.ArrayBuffer;
    }

    public void Dispose()
    {
        GLErrorChecker.ValidateHandle(Handle, "Buffer");
        _gl.DeleteBuffer(Handle);
        GLErrorChecker.CheckError(_gl, "DeleteBuffer");
    }
}
