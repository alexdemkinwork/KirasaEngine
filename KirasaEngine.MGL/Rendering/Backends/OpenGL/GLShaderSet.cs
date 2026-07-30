using Silk.NET.OpenGL;

using KirasaEngine.MGL.Rendering;

namespace KirasaEngine.MGL.Rendering.Backends.OpenGL;

internal sealed class GLShaderSet : IShaderSet
{
    private readonly GL _gl;

    public uint ProgramHandle { get; }
    public string ShaderName { get; }
    public VertexLayoutDescription[] VertexLayouts { get; }

    public GLShaderSet(GL gl, ShaderSetDescription description)
    {
        _gl = gl;
        ShaderName = description.ShaderName;
        VertexLayouts = description.VertexLayouts;

        var vertexSource = Rendering.ShaderLibrary.ShaderLibrary.GetGlslSource(description.ShaderName, ShaderStage.Vertex);
        var fragmentSource = Rendering.ShaderLibrary.ShaderLibrary.GetGlslSource(description.ShaderName, ShaderStage.Fragment);

        var vertexShader = CompileStage(ShaderType.VertexShader, vertexSource);
        var fragmentShader = CompileStage(ShaderType.FragmentShader, fragmentSource);

        ProgramHandle = _gl.CreateProgram();
        _gl.AttachShader(ProgramHandle, vertexShader);
        _gl.AttachShader(ProgramHandle, fragmentShader);
        _gl.LinkProgram(ProgramHandle);

        _gl.GetProgram(ProgramHandle, GLEnum.LinkStatus, out var linkStatus);
        if (linkStatus == 0)
        {
            var log = _gl.GetProgramInfoLog(ProgramHandle);
            _gl.DeleteProgram(ProgramHandle);
            throw new InvalidOperationException($"OpenGL program link failed for shader '{ShaderName}': {log}");
        }

        _gl.DetachShader(ProgramHandle, vertexShader);
        _gl.DetachShader(ProgramHandle, fragmentShader);
        _gl.DeleteShader(vertexShader);
        _gl.DeleteShader(fragmentShader);
    }

    private uint CompileStage(ShaderType type, string source)
    {
        var handle = _gl.CreateShader(type);
        _gl.ShaderSource(handle, source);
        _gl.CompileShader(handle);

        _gl.GetShader(handle, GLEnum.CompileStatus, out var compileStatus);
        if (compileStatus == 0)
        {
            var log = _gl.GetShaderInfoLog(handle);
            _gl.DeleteShader(handle);
            throw new InvalidOperationException($"OpenGL {type} compile failed for shader '{ShaderName}': {log}");
        }

        return handle;
    }

    public void Dispose() => _gl.DeleteProgram(ProgramHandle);
}
