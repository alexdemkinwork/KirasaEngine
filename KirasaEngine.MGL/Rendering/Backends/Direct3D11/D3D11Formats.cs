using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;
using Silk.NET.DXGI;

namespace KirasaEngine.MGL.Rendering.Backends.Direct3D11;

/// <summary>
/// Maps the backend-agnostic Abstractions enums onto their DXGI/Direct3D11 equivalents.
/// Mirrors <c>GLFormats</c> in the OpenGL backend.
/// </summary>
internal static class D3D11Formats
{
    public static bool IsDepthFormat(TextureFormat format) =>
        format is TextureFormat.Depth24Stencil8 or TextureFormat.Depth32Float;

    /// <summary>
    /// Storage format of the underlying ID3D11Texture2D. Depth textures that are also sampled must be
    /// created typeless so that the DSV and the SRV can pick their own (incompatible) concrete formats.
    /// </summary>
    public static Format MapResource(TextureFormat format, TextureUsage usage)
    {
        if (IsDepthFormat(format) && usage.HasFlag(TextureUsage.Sampled))
        {
            return format switch
            {
                TextureFormat.Depth24Stencil8 => Format.FormatR24G8Typeless,
                TextureFormat.Depth32Float => Format.FormatR32Typeless,
                _ => throw new ArgumentOutOfRangeException(nameof(format)),
            };
        }

        return MapConcrete(format);
    }

    /// <summary>The plain (non-typeless) DXGI format for a <see cref="TextureFormat"/>.</summary>
    public static Format MapConcrete(TextureFormat format) => format switch
    {
        TextureFormat.Rgba8UNorm => Format.FormatR8G8B8A8Unorm,
        TextureFormat.Bgra8UNorm => Format.FormatB8G8R8A8Unorm,
        TextureFormat.R32Float => Format.FormatR32Float,
        TextureFormat.Rgba16Float => Format.FormatR16G16B16A16Float,
        TextureFormat.Depth24Stencil8 => Format.FormatD24UnormS8Uint,
        TextureFormat.Depth32Float => Format.FormatD32Float,
        _ => throw new ArgumentOutOfRangeException(nameof(format)),
    };

    public static Format MapDsv(TextureFormat format) => format switch
    {
        TextureFormat.Depth24Stencil8 => Format.FormatD24UnormS8Uint,
        TextureFormat.Depth32Float => Format.FormatD32Float,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Not a depth format."),
    };

    public static Format MapSrv(TextureFormat format) => format switch
    {
        TextureFormat.Depth24Stencil8 => Format.FormatR24UnormX8Typeless,
        TextureFormat.Depth32Float => Format.FormatR32Float,
        _ => MapConcrete(format),
    };

    public static Format MapRtv(TextureFormat format) => MapConcrete(format);

    public static uint BytesPerPixel(TextureFormat format) => format switch
    {
        TextureFormat.Rgba8UNorm => 4,
        TextureFormat.Bgra8UNorm => 4,
        TextureFormat.R32Float => 4,
        TextureFormat.Rgba16Float => 8,
        TextureFormat.Depth24Stencil8 => 4,
        TextureFormat.Depth32Float => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(format)),
    };

    public static Format MapVertexElement(VertexElementFormat format) => format switch
    {
        VertexElementFormat.Float1 => Format.FormatR32Float,
        VertexElementFormat.Float2 => Format.FormatR32G32Float,
        VertexElementFormat.Float3 => Format.FormatR32G32B32Float,
        VertexElementFormat.Float4 => Format.FormatR32G32B32A32Float,
        _ => throw new ArgumentOutOfRangeException(nameof(format)),
    };

    public static Format MapIndexFormat(IndexFormat format) => format switch
    {
        IndexFormat.UInt16 => Format.FormatR16Uint,
        IndexFormat.UInt32 => Format.FormatR32Uint,
        _ => throw new ArgumentOutOfRangeException(nameof(format)),
    };

    public static uint IndexSizeInBytes(IndexFormat format) => format == IndexFormat.UInt32 ? 4u : 2u;

    public static D3DPrimitiveTopology MapTopology(PrimitiveTopology topology) => topology switch
    {
        PrimitiveTopology.TriangleList => D3DPrimitiveTopology.D3DPrimitiveTopologyTrianglelist,
        PrimitiveTopology.TriangleStrip => D3DPrimitiveTopology.D3DPrimitiveTopologyTrianglestrip,
        PrimitiveTopology.LineList => D3DPrimitiveTopology.D3DPrimitiveTopologyLinelist,
        PrimitiveTopology.PointList => D3DPrimitiveTopology.D3DPrimitiveTopologyPointlist,
        _ => throw new ArgumentOutOfRangeException(nameof(topology)),
    };

    public static ComparisonFunc MapCompare(CompareFunction compare) => compare switch
    {
        CompareFunction.Never => ComparisonFunc.Never,
        CompareFunction.Less => ComparisonFunc.Less,
        CompareFunction.Equal => ComparisonFunc.Equal,
        CompareFunction.LessEqual => ComparisonFunc.LessEqual,
        CompareFunction.Greater => ComparisonFunc.Greater,
        CompareFunction.NotEqual => ComparisonFunc.NotEqual,
        CompareFunction.GreaterEqual => ComparisonFunc.GreaterEqual,
        CompareFunction.Always => ComparisonFunc.Always,
        _ => throw new ArgumentOutOfRangeException(nameof(compare)),
    };

    public static Silk.NET.Direct3D11.CullMode MapCullMode(Abstractions.Enums.CullMode mode) => mode switch
    {
        Abstractions.Enums.CullMode.None => Silk.NET.Direct3D11.CullMode.None,
        Abstractions.Enums.CullMode.Front => Silk.NET.Direct3D11.CullMode.Front,
        Abstractions.Enums.CullMode.Back => Silk.NET.Direct3D11.CullMode.Back,
        _ => throw new ArgumentOutOfRangeException(nameof(mode)),
    };

    public static Silk.NET.Direct3D11.FillMode MapFillMode(Abstractions.Enums.FillMode mode) => mode switch
    {
        Abstractions.Enums.FillMode.Solid => Silk.NET.Direct3D11.FillMode.Solid,
        Abstractions.Enums.FillMode.Wireframe => Silk.NET.Direct3D11.FillMode.Wireframe,
        _ => throw new ArgumentOutOfRangeException(nameof(mode)),
    };

    public static Filter MapFilter(SamplerFilter filter) =>
        filter == SamplerFilter.Linear ? Filter.MinMagMipLinear : Filter.MinMagMipPoint;

    public static TextureAddressMode MapAddressMode(SamplerAddressMode mode) => mode switch
    {
        SamplerAddressMode.Wrap => TextureAddressMode.Wrap,
        SamplerAddressMode.Clamp => TextureAddressMode.Clamp,
        SamplerAddressMode.Mirror => TextureAddressMode.Mirror,
        _ => throw new ArgumentOutOfRangeException(nameof(mode)),
    };
}
