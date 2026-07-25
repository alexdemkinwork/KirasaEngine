namespace KirasaEngine.Render.Domain.Models.Render.Modificators;

public abstract class BaseModificator<TParentRenderNode>
{
    protected readonly TParentRenderNode _parentRenderNode;
    public BaseModificator(TParentRenderNode parentRenderNode) => _parentRenderNode = parentRenderNode;
}