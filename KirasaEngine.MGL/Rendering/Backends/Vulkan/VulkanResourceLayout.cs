using Silk.NET.Vulkan;

namespace KirasaEngine.MGL.Rendering.Backends.Vulkan;

/// <summary>
/// A <see cref="DescriptorSetLayout"/> for descriptor set 0, plus the pool its sets are allocated from.
/// </summary>
/// <remarks>
/// <para><b>Binding numbers come from each element's array index, not from
/// <see cref="ResourceLayoutElementDescription.Binding"/>.</b> A Vulkan descriptor set has a single flat
/// binding-number space shared by every descriptor type, whereas GL/D3D give each resource kind its own
/// namespace — so <c>ShaderResourceLayouts.Standard</c>'s values (0, 1, 0, 0) would collide here. Index
/// order (0 = FrameConstants UBO, 1 = DrawConstants UBO, 2 = BaseColorTexture, 3 = BaseColorSampler)
/// matches what VulkanGLSL/Standard.vert/frag declare. See ShaderResourceLayouts' doc comment.</para>
/// <para>The pool is created with FREE_DESCRIPTOR_SET so <see cref="VulkanResourceSet.Dispose"/> can hand
/// sets back individually: SceneRenderer allocates one set per draw batch per frame and disposes it right
/// after Submit, which would otherwise exhaust the pool over repeated frames.</para>
/// </remarks>
internal sealed unsafe class VulkanResourceLayout : IResourceLayout
{
    private const uint MaxSets = 256;

    private readonly VulkanContext _context;

    public ResourceLayoutDescription Description { get; }
    public DescriptorSetLayout Handle { get; }
    public DescriptorPool Pool { get; }

    public VulkanResourceLayout(VulkanContext context, ResourceLayoutDescription description)
    {
        _context = context;
        Description = description;

        var elements = description.Elements;
        var bindings = stackalloc DescriptorSetLayoutBinding[elements.Length];
        for (var i = 0; i < elements.Length; i++)
        {
            bindings[i] = new DescriptorSetLayoutBinding
            {
                Binding = (uint)i,
                DescriptorType = VulkanFormats.MapResourceKind(elements[i].Kind),
                DescriptorCount = 1,
                StageFlags = VulkanFormats.MapShaderStages(elements[i].Stages),
            };
        }

        var layoutInfo = new DescriptorSetLayoutCreateInfo
        {
            SType = StructureType.DescriptorSetLayoutCreateInfo,
            BindingCount = (uint)elements.Length,
            PBindings = bindings,
        };

        DescriptorSetLayout layout;
        VulkanUtil.Check(context.Vk.CreateDescriptorSetLayout(context.Device, &layoutInfo, null, &layout), "vkCreateDescriptorSetLayout");
        Handle = layout;

        var poolSizes = stackalloc DescriptorPoolSize[elements.Length];
        for (var i = 0; i < elements.Length; i++)
        {
            poolSizes[i] = new DescriptorPoolSize
            {
                Type = VulkanFormats.MapResourceKind(elements[i].Kind),
                DescriptorCount = MaxSets,
            };
        }

        var poolInfo = new DescriptorPoolCreateInfo
        {
            SType = StructureType.DescriptorPoolCreateInfo,
            Flags = DescriptorPoolCreateFlags.FreeDescriptorSetBit,
            MaxSets = MaxSets,
            PoolSizeCount = (uint)elements.Length,
            PPoolSizes = poolSizes,
        };

        DescriptorPool pool;
        VulkanUtil.Check(context.Vk.CreateDescriptorPool(context.Device, &poolInfo, null, &pool), "vkCreateDescriptorPool");
        Pool = pool;
    }

    public void Dispose()
    {
        _context.Vk.DestroyDescriptorPool(_context.Device, Pool, null);
        _context.Vk.DestroyDescriptorSetLayout(_context.Device, Handle, null);
    }
}
