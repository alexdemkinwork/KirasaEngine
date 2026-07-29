namespace KirasaEngine.MGL.Rendering.Abstractions.Descriptions;

public sealed class PipelineDescription
{
    public required IShaderSet ShaderSet { get; init; }
    public required IResourceLayout ResourceLayout { get; init; }
    public PrimitiveTopology Topology { get; init; } = PrimitiveTopology.TriangleList;
    public CullMode CullMode { get; init; } = CullMode.Back;
    public FillMode FillMode { get; init; } = FillMode.Solid;
    public FrontFace FrontFace { get; init; } = FrontFace.CounterClockwise;
    public bool DepthTestEnabled { get; init; } = true;
    public bool DepthWriteEnabled { get; init; } = true;
    public CompareFunction DepthCompare { get; init; } = CompareFunction.LessEqual;
    public BlendMode Blend { get; init; } = BlendMode.Opaque;
    public required TextureFormat ColorFormat { get; init; }
    public TextureFormat? DepthFormat { get; init; } = TextureFormat.Depth24Stencil8;
}
