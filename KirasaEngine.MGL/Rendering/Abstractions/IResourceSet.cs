namespace KirasaEngine.MGL.Rendering.Abstractions;

public interface IResourceSet : IDisposable
{
    IResourceLayout Layout { get; }
}
