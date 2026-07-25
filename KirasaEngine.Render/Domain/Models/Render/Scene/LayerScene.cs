namespace KirasaEngine.Render.Domain.Models.Render.Scene;

public class LayerScene
{
    public List<RenderNode> Nodes { get; private set; }

    public LayerScene(List<RenderNode> nodes) => Nodes = nodes;
    public void AddNode(RenderNode node)
    {
        Nodes.Add(node);
        Nodes.Sort();
    }
    public void RemoveNode(Guid id) => Nodes.Remove(Nodes.Where(x=>x.IdNode.Equals(id))!.FirstOrDefault()!);
}