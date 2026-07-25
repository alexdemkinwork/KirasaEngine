

namespace KirasaEngine.Render.Domain.Models.Render.Modificators;

public class LineModificator : BaseModificator<RenderNodeModificator<LineModificator>>
{
    public Vector2 Point1 { get; set; }
    public Vector2 Point2 { get; set; }
    public float Stroke { get; set; } = 1.0f;
    public int? DashLength { get; set; }
    public int? DashSpacing { get; set; }
    public float? BezierLength { get; set; }
    public LineType LineType { get; set; }

    public LineModificator(RenderNodeModificator<LineModificator> parentRenderNode) : base(parentRenderNode)
    {
        
    }
}