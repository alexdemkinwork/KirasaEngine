namespace KirasaEngine.MGL.Rendering.Abstractions.Structs;

public readonly struct Viewport(float x, float y, float width, float height, float minDepth = 0f, float maxDepth = 1f)
{
    public readonly float X = x;
    public readonly float Y = y;
    public readonly float Width = width;
    public readonly float Height = height;
    public readonly float MinDepth = minDepth;
    public readonly float MaxDepth = maxDepth;
}
