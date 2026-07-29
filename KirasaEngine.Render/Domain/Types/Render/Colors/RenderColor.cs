namespace KirasaEngine.Render.Domain.Types.Render.Colors;

public struct RenderColor
{
    public string Hex { get; set; } = "#000000";
    public float Opacity { get; set; } = 1;
    public RenderColor(string hex) => Hex = hex;
}