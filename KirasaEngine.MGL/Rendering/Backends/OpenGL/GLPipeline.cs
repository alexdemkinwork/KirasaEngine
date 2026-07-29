using Silk.NET.OpenGL;

namespace KirasaEngine.MGL.Rendering.Backends.OpenGL;

internal sealed class GLPipeline : IPipeline
{
    private readonly GL _gl;

    public PipelineDescription Description { get; }
    public GLShaderSet ShaderSet { get; }
    public uint VertexArrayHandle { get; }

    public GLPipeline(GL gl, PipelineDescription description)
    {
        _gl = gl;
        Description = description;
        ShaderSet = (GLShaderSet)description.ShaderSet;
        VertexArrayHandle = _gl.GenVertexArray();
    }

    public void Dispose() => _gl.DeleteVertexArray(VertexArrayHandle);
}
