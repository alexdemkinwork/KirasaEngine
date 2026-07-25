namespace KirasaEngine.Render.Domain.Types.Render;

public struct RenderColor
{
    public string Hex { get; set; } = "#000000";
    public float Opacity { get; set; } = 1;
    public RenderColor(string hex) => Hex = hex;
}