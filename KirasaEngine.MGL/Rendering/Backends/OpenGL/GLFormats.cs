using Silk.NET.OpenGL;

using KirasaEngine.MGL.Rendering;

namespace KirasaEngine.MGL.Rendering.Backends.OpenGL;

internal static class GLFormats
{
    public static (InternalFormat Internal, PixelFormat Pixel, PixelType Type) MapTexture(TextureFormat format) => format switch
    {
        TextureFormat.Rgba8UNorm => (InternalFormat.Rgba8, PixelFormat.Rgba, PixelType.UnsignedByte),
        TextureFormat.Bgra8UNorm => (InternalFormat.Rgba8, PixelFormat.Bgra, PixelType.UnsignedByte),
        TextureFormat.R32Float => (InternalFormat.R32f, PixelFormat.Red, PixelType.Float),
        TextureFormat.Rgba16Float => (InternalFormat.Rgba16f, PixelFormat.Rgba, PixelType.HalfFloat),
        TextureFormat.Depth24Stencil8 => (InternalFormat.Depth24Stencil8, PixelFormat.DepthStencil, PixelType.UnsignedInt248),
        TextureFormat.Depth32Float => (InternalFormat.DepthComponent32f, PixelFormat.DepthComponent, PixelType.Float),
        _ => throw new ArgumentOutOfRangeException(nameof(format)),
    };

    public static bool IsDepthFormat(TextureFormat format) =>
        format is TextureFormat.Depth24Stencil8 or TextureFormat.Depth32Float;

    public static (int Count, VertexAttribPointerType Type) MapVertexElement(VertexElementFormat format) => format switch
    {
        VertexElementFormat.Float1 => (1, VertexAttribPointerType.Float),
        VertexElementFormat.Float2 => (2, VertexAttribPointerType.Float),
        VertexElementFormat.Float3 => (3, VertexAttribPointerType.Float),
        VertexElementFormat.Float4 => (4, VertexAttribPointerType.Float),
        _ => throw new ArgumentOutOfRangeException(nameof(format)),
    };

    public static DrawElementsType MapIndexFormat(IndexFormat format) => format switch
    {
        IndexFormat.UInt16 => DrawElementsType.UnsignedShort,
        IndexFormat.UInt32 => DrawElementsType.UnsignedInt,
        _ => throw new ArgumentOutOfRangeException(nameof(format)),
    };

    public static PrimitiveType MapTopology(PrimitiveTopology topology) => topology switch
    {
        PrimitiveTopology.TriangleList => PrimitiveType.Triangles,
        PrimitiveTopology.TriangleStrip => PrimitiveType.TriangleStrip,
        PrimitiveTopology.LineList => PrimitiveType.Lines,
        PrimitiveTopology.PointList => PrimitiveType.Points,
        _ => throw new ArgumentOutOfRangeException(nameof(topology)),
    };

    public static DepthFunction MapCompare(CompareFunction compare) => compare switch
    {
        CompareFunction.Never => DepthFunction.Never,
        CompareFunction.Less => DepthFunction.Less,
        CompareFunction.Equal => DepthFunction.Equal,
        CompareFunction.LessEqual => DepthFunction.Lequal,
        CompareFunction.Greater => DepthFunction.Greater,
        CompareFunction.NotEqual => DepthFunction.Notequal,
        CompareFunction.GreaterEqual => DepthFunction.Gequal,
        CompareFunction.Always => DepthFunction.Always,
        _ => throw new ArgumentOutOfRangeException(nameof(compare)),
    };

    public static TextureMinFilter MapMinFilter(SamplerFilter filter) =>
        filter == SamplerFilter.Linear ? TextureMinFilter.Linear : TextureMinFilter.Nearest;

    public static TextureMagFilter MapMagFilter(SamplerFilter filter) =>
        filter == SamplerFilter.Linear ? TextureMagFilter.Linear : TextureMagFilter.Nearest;

    public static GLEnum MapAddressMode(SamplerAddressMode mode) => mode switch
    {
        SamplerAddressMode.Wrap => GLEnum.Repeat,
        SamplerAddressMode.Clamp => GLEnum.ClampToEdge,
        SamplerAddressMode.Mirror => GLEnum.MirroredRepeat,
        _ => throw new ArgumentOutOfRangeException(nameof(mode)),
    };
}
