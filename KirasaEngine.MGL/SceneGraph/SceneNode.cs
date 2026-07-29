namespace KirasaEngine.MGL.SceneGraph;

public sealed class SceneNode
{
    private readonly List<SceneNode> _children = [];

    public string Name { get; set; } = string.Empty;
    public Transform Transform { get; } = new();
    public MeshRenderer? Renderer { get; set; }
    public Camera? Camera { get; set; }
    public Light? Light { get; set; }

    public SceneNode? Parent { get; private set; }
    public IReadOnlyList<SceneNode> Children => _children;

    public SceneNode AddChild(SceneNode child)
    {
        child.Parent?._children.Remove(child);
        child.Parent = this;
        child.Transform.Parent = Transform;
        _children.Add(child);
        return child;
    }

    public void RemoveChild(SceneNode child)
    {
        if (!_children.Remove(child)) return;
        child.Parent = null;
        child.Transform.Parent = null;
    }

    public IEnumerable<SceneNode> Traverse()
    {
        yield return this;
        foreach (var child in _children)
        foreach (var descendant in child.Traverse())
            yield return descendant;
    }
}
