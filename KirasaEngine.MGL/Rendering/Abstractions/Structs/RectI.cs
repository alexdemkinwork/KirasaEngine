namespace KirasaEngine.MGL.Rendering.Abstractions.Structs;

public readonly struct RectI(int x, int y, int width, int height)
{
    public readonly int X = x;
    public readonly int Y = y;
    public readonly int Width = width;
    public readonly int Height = height;
}
