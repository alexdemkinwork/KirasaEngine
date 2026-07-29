namespace KirasaEngine.MGL.Models;

/// <summary>
/// Pure CPU-side mesh data. GPU buffers are not owned here — they are lazily created and cached per
/// <see cref="Rendering.Abstractions.IGraphicsDevice"/> by the renderer, so a Mesh survives a backend switch.
/// </summary>
public sealed class Mesh
{
    public required VertexPNCT[] Vertices { get; init; }
    public required uint[] Indices { get; init; }

    /// <summary>Bumped whenever CPU data changes so cached GPU buffers can be invalidated.</summary>
    public int Version { get; private set; }

    public void MarkChanged() => Version++;
}
