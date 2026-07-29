namespace KirasaEngine.MGL.SceneGraph;

public sealed class Scene
{
    public SceneNode Root { get; } = new() { Name = "Root" };
    public Vector4 BackgroundColor { get; set; } = new(0.1f, 0.12f, 0.15f, 1f);
    public Vector3 AmbientColor { get; set; } = new(0.15f, 0.15f, 0.15f);

    /// <summary>Instance batches not tied to the node hierarchy (procedural crowds, foliage, etc).</summary>
    public List<InstancedBatch> InstancedBatches { get; } = [];

    public IEnumerable<SceneNode> Traverse() => Root.Traverse();

    public Camera? FindFirstCamera() => Traverse().FirstOrDefault(n => n.Camera is not null)?.Camera;

    public SceneNode? FindCameraNode() => Traverse().FirstOrDefault(n => n.Camera is not null);

    public IEnumerable<SceneNode> FindLightNodes() => Traverse().Where(n => n.Light is not null);
}
