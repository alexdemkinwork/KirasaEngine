namespace KirasaEngine.MGL.Instancing;

/// <summary>An explicitly user-managed group of instances sharing one mesh+material (e.g. grass, crowds, foliage).</summary>
public sealed class InstancedBatch
{
    private readonly List<InstanceData> _instances = [];

    public required Mesh Mesh { get; init; }
    public required Material Material { get; init; }
    public IReadOnlyList<InstanceData> Instances => _instances;

    /// <summary>Set by the renderer once the GPU instance buffer has been refreshed for the current contents.</summary>
    public bool Dirty { get; private set; } = true;

    public void SetInstances(IReadOnlyList<InstanceData> instances)
    {
        _instances.Clear();
        _instances.AddRange(instances);
        Dirty = true;
    }

    public void AddInstance(InstanceData instance)
    {
        _instances.Add(instance);
        Dirty = true;
    }

    public void ClearInstances()
    {
        _instances.Clear();
        Dirty = true;
    }

    internal void ClearDirty() => Dirty = false;
}
