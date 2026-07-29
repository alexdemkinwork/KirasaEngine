namespace KirasaEngine.MGL.Rendering.Abstractions;

public interface IShaderSet : IDisposable
{
    string ShaderName { get; }
    VertexLayoutDescription[] VertexLayouts { get; }
}
