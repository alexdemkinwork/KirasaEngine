namespace KirasaEngine.Render.Domain.Models.Render.Scene;

public class RenderScene
{
    public required int WidthResolution { get; set; }
    public required int HeightResolution { get; set; }
    public required string Title { get; set; }
    public required bool RenderTexture { get; set; } 
    public required bool ShowFrame { get; set; }
    public required RenderColor BackgroundColor { get; set; }
    public required TypeBackendRender TypeBackend { get; set; }
    public required Dictionary<string, LayerScene> Layers { get; set; }
}