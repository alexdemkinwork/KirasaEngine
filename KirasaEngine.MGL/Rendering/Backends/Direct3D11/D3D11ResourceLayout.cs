using KirasaEngine.MGL.Rendering;

namespace KirasaEngine.MGL.Rendering.Backends.Direct3D11;

/// <summary>
/// D3D11 has no descriptor-set object: bindings are set individually per shader stage at draw time, so the
/// layout is pure metadata (exactly as in the OpenGL backend).
/// </summary>
internal sealed class D3D11ResourceLayout(ResourceLayoutDescription description) : IResourceLayout
{
    public ResourceLayoutDescription Description { get; } = description;

    public void Dispose() { }
}
