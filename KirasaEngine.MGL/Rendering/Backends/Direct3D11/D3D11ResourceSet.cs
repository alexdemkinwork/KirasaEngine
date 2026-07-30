using KirasaEngine.MGL.Rendering;

namespace KirasaEngine.MGL.Rendering.Backends.Direct3D11;

internal sealed class D3D11ResourceSet(ResourceSetDescription description) : IResourceSet
{
    public IResourceLayout Layout { get; } = description.Layout;
    public object[] Resources { get; } = description.Resources;

    public void Dispose() { }
}
