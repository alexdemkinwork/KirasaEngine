namespace KirasaEngine.MGL.Rendering.Abstractions.Structs;

/// <summary>Describes the layout of a single bound vertex buffer slot (either per-vertex mesh data or per-instance data).</summary>
public readonly struct VertexLayoutDescription(uint stride, VertexInputRate inputRate, params VertexElementDescription[] elements)
{
    public readonly uint Stride = stride;
    public readonly VertexInputRate InputRate = inputRate;
    public readonly VertexElementDescription[] Elements = elements;
}
