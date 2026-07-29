using Silk.NET.Vulkan;
using AbsPrimitiveTopology = KirasaEngine.MGL.Rendering.Abstractions.Enums.PrimitiveTopology;
using AbsSamplerAddressMode = KirasaEngine.MGL.Rendering.Abstractions.Enums.SamplerAddressMode;
using AbsFrontFace = KirasaEngine.MGL.Rendering.Abstractions.Enums.FrontFace;

namespace KirasaEngine.MGL.Rendering.Backends.Vulkan;

/// <summary>Vulkan counterpart of <c>GLFormats</c>: pure translation of abstraction enums to Silk.NET.Vulkan enums.</summary>
internal static class VulkanFormats
{
    /// <summary>
    /// Depth24Stencil8 is resolved by <see cref="VulkanContext.MapFormat"/> against the physical device
    /// (some GPUs only expose D32_SFLOAT_S8_UINT), so this method returns the nominal mapping only.
    /// </summary>
    public static Format MapTexture(TextureFormat format) => format switch
    {
        TextureFormat.Rgba8UNorm => Format.R8G8B8A8Unorm,
        TextureFormat.Bgra8UNorm => Format.B8G8R8A8Unorm,
        TextureFormat.R32Float => Format.R32Sfloat,
        TextureFormat.Rgba16Float => Format.R16G16B16A16Sfloat,
        TextureFormat.Depth24Stencil8 => Format.D24UnormS8Uint,
        TextureFormat.Depth32Float => Format.D32Sfloat,
        _ => throw new ArgumentOutOfRangeException(nameof(format)),
    };

    public static bool IsDepthFormat(TextureFormat format) =>
        format is TextureFormat.Depth24Stencil8 or TextureFormat.Depth32Float;

    public static bool HasStencil(Format format) =>
        format is Format.D24UnormS8Uint or Format.D32SfloatS8Uint or Format.D16UnormS8Uint or Format.S8Uint;

    public static Format MapVertexElement(VertexElementFormat format) => format switch
    {
        VertexElementFormat.Float1 => Format.R32Sfloat,
        VertexElementFormat.Float2 => Format.R32G32Sfloat,
        VertexElementFormat.Float3 => Format.R32G32B32Sfloat,
        VertexElementFormat.Float4 => Format.R32G32B32A32Sfloat,
        _ => throw new ArgumentOutOfRangeException(nameof(format)),
    };

    public static IndexType MapIndexFormat(IndexFormat format) => format switch
    {
        IndexFormat.UInt16 => IndexType.Uint16,
        IndexFormat.UInt32 => IndexType.Uint32,
        _ => throw new ArgumentOutOfRangeException(nameof(format)),
    };

    public static Silk.NET.Vulkan.PrimitiveTopology MapTopology(AbsPrimitiveTopology topology) => topology switch
    {
        AbsPrimitiveTopology.TriangleList => Silk.NET.Vulkan.PrimitiveTopology.TriangleList,
        AbsPrimitiveTopology.TriangleStrip => Silk.NET.Vulkan.PrimitiveTopology.TriangleStrip,
        AbsPrimitiveTopology.LineList => Silk.NET.Vulkan.PrimitiveTopology.LineList,
        AbsPrimitiveTopology.PointList => Silk.NET.Vulkan.PrimitiveTopology.PointList,
        _ => throw new ArgumentOutOfRangeException(nameof(topology)),
    };

    public static CompareOp MapCompare(CompareFunction compare) => compare switch
    {
        CompareFunction.Never => CompareOp.Never,
        CompareFunction.Less => CompareOp.Less,
        CompareFunction.Equal => CompareOp.Equal,
        CompareFunction.LessEqual => CompareOp.LessOrEqual,
        CompareFunction.Greater => CompareOp.Greater,
        CompareFunction.NotEqual => CompareOp.NotEqual,
        CompareFunction.GreaterEqual => CompareOp.GreaterOrEqual,
        CompareFunction.Always => CompareOp.Always,
        _ => throw new ArgumentOutOfRangeException(nameof(compare)),
    };

    public static CullModeFlags MapCullMode(CullMode mode) => mode switch
    {
        CullMode.None => CullModeFlags.None,
        CullMode.Front => CullModeFlags.FrontBit,
        CullMode.Back => CullModeFlags.BackBit,
        _ => throw new ArgumentOutOfRangeException(nameof(mode)),
    };

    /// <summary>
    /// Vulkan's front-face winding is evaluated in framebuffer coordinates. The backend renders with a
    /// negative-height viewport (see <c>VulkanCommandList.SetViewport</c>), which flips framebuffer Y back
    /// to the GL/D3D orientation, so the abstraction's winding maps across one-to-one with no inversion.
    /// </summary>
    public static Silk.NET.Vulkan.FrontFace MapFrontFace(AbsFrontFace frontFace) => frontFace switch
    {
        AbsFrontFace.Clockwise => Silk.NET.Vulkan.FrontFace.Clockwise,
        AbsFrontFace.CounterClockwise => Silk.NET.Vulkan.FrontFace.CounterClockwise,
        _ => throw new ArgumentOutOfRangeException(nameof(frontFace)),
    };

    public static PolygonMode MapFillMode(FillMode mode) => mode switch
    {
        FillMode.Solid => PolygonMode.Fill,
        FillMode.Wireframe => PolygonMode.Line,
        _ => throw new ArgumentOutOfRangeException(nameof(mode)),
    };

    public static Filter MapFilter(SamplerFilter filter) =>
        filter == SamplerFilter.Linear ? Filter.Linear : Filter.Nearest;

    public static SamplerMipmapMode MapMipmapMode(SamplerFilter filter) =>
        filter == SamplerFilter.Linear ? SamplerMipmapMode.Linear : SamplerMipmapMode.Nearest;

    public static Silk.NET.Vulkan.SamplerAddressMode MapAddressMode(AbsSamplerAddressMode mode) => mode switch
    {
        AbsSamplerAddressMode.Wrap => Silk.NET.Vulkan.SamplerAddressMode.Repeat,
        AbsSamplerAddressMode.Clamp => Silk.NET.Vulkan.SamplerAddressMode.ClampToEdge,
        AbsSamplerAddressMode.Mirror => Silk.NET.Vulkan.SamplerAddressMode.MirroredRepeat,
        _ => throw new ArgumentOutOfRangeException(nameof(mode)),
    };

    public static Silk.NET.Vulkan.VertexInputRate MapInputRate(Abstractions.Enums.VertexInputRate rate) =>
        rate == Abstractions.Enums.VertexInputRate.PerInstance ? Silk.NET.Vulkan.VertexInputRate.Instance : Silk.NET.Vulkan.VertexInputRate.Vertex;

    public static ShaderStageFlags MapShaderStages(ShaderStage stages)
    {
        var flags = ShaderStageFlags.None;
        if (stages.HasFlag(ShaderStage.Vertex)) flags |= ShaderStageFlags.VertexBit;
        if (stages.HasFlag(ShaderStage.Fragment)) flags |= ShaderStageFlags.FragmentBit;
        return flags;
    }

    public static DescriptorType MapResourceKind(ResourceKind kind) => kind switch
    {
        // VulkanGLSL/Standard.frag declares `texture2D` + `sampler` separately (not a combined sampler2D),
        // so each ResourceLayoutElement maps 1:1 onto one descriptor, exactly like the other backends.
        ResourceKind.UniformBuffer => DescriptorType.UniformBuffer,
        ResourceKind.TextureReadOnly => DescriptorType.SampledImage,
        ResourceKind.Sampler => DescriptorType.Sampler,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}
