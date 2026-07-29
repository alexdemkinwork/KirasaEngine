namespace KirasaEngine.MGL.Rendering.Abstractions.Enums;

public enum PrimitiveTopology
{
    TriangleList,
    TriangleStrip,
    LineList,
    PointList,
}

public enum CullMode
{
    None,
    Front,
    Back,
}

public enum FillMode
{
    Solid,
    Wireframe,
}

public enum FrontFace
{
    Clockwise,
    CounterClockwise,
}

/// <summary>Pragmatic preset blend modes covering the common cases instead of a full fixed-function blend-equation config.</summary>
public enum BlendMode
{
    Opaque,
    AlphaBlend,
    Additive,
}

public enum CompareFunction
{
    Never,
    Less,
    Equal,
    LessEqual,
    Greater,
    NotEqual,
    GreaterEqual,
    Always,
}
