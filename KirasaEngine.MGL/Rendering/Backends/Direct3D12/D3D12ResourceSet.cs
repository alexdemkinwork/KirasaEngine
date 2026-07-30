using KirasaEngine.MGL.Rendering;

namespace KirasaEngine.MGL.Rendering.Backends.Direct3D12;

/// <summary>
/// Unlike Vulkan there is no descriptor-set object to allocate here: every descriptor this set refers to
/// already lives in a device-owned heap (SRVs and samplers) or is addressed by GPU virtual address (root
/// CBVs). So the set is just the resource list, resolved to concrete root arguments at bind time by
/// <see cref="D3D12CommandList.SetResourceSet"/>.
///
/// <para>Deliberately not caching the addresses/handles at construction: a <see cref="D3D12Buffer"/>'s GPU
/// virtual address is read fresh on every bind, which keeps the set valid even if a buffer is recreated.</para>
/// </summary>
internal sealed class D3D12ResourceSet(ResourceSetDescription description) : IResourceSet
{
    public IResourceLayout Layout { get; } = description.Layout;
    public object[] Resources { get; } = description.Resources;

    public void Dispose() { }
}
