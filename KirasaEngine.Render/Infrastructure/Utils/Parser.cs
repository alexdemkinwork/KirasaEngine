namespace KirasaEngine.Render.Infrastructure.Utils;

public static class Parser
{
    public static Rectangle Vector4ToRectangle(Vector4 vector4) => new Rectangle(vector4.X, vector4.Y, vector4.Z, vector4.W);
    public static Vector4 RectangleToVector4(Rectangle rect) => new Vector4(rect.X, rect.Y, rect.Width, rect.Height);
}