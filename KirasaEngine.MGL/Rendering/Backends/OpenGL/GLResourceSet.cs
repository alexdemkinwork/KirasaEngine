namespace KirasaEngine.MGL.Rendering.Backends.OpenGL;

internal sealed class GLResourceSet(ResourceSetDescription description) : IResourceSet
{
    public IResourceLayout Layout { get; } = description.Layout;
    public object[] Resources { get; } = description.Resources;

    public void Dispose() { }
}
