using KirasaEngine.Render.Domain.Enums.Render.Modificators;

namespace KirasaEngine.Render.Domain.Models.Render.Modificators;

public class LinesModificator : BaseModificator<RenderNodeModificator<LinesModificator>>
{
    public List<RenderNodeModificator<LineModificator>> Lines { get; set; }
    public LinesModificator(RenderNodeModificator<LinesModificator> parentRenderNode) : base(parentRenderNode)
    {
        
    }
}