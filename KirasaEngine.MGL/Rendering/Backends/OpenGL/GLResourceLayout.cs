using KirasaEngine.MGL.Rendering;

namespace KirasaEngine.MGL.Rendering.Backends.OpenGL;

internal sealed class GLResourceLayout(ResourceLayoutDescription description) : IResourceLayout
{
    public ResourceLayoutDescription Description { get; } = description;

    public void Dispose() { }
}
