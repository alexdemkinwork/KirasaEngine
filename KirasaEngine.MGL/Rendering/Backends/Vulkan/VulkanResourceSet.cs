using Silk.NET.Vulkan;

using KirasaEngine.MGL.Rendering;

namespace KirasaEngine.MGL.Rendering.Backends.Vulkan;

/// <summary>
/// One <see cref="DescriptorSet"/> allocated from its layout's pool and immediately written to point at the
/// concrete resources. Binding numbers are the element array indices — see <see cref="VulkanResourceLayout"/>.
/// </summary>
internal sealed unsafe class VulkanResourceSet : IResourceSet
{
    private readonly VulkanContext _context;
    private readonly VulkanResourceLayout _layout;

    public IResourceLayout Layout => _layout;
    public DescriptorSet Handle { get; }

    public VulkanResourceSet(VulkanContext context, ResourceSetDescription description)
    {
        _context = context;
        _layout = (VulkanResourceLayout)description.Layout;

        var elements = _layout.Description.Elements;
        var resources = description.Resources;
        if (resources.Length != elements.Length)
            throw new ArgumentException($"Resource set provides {resources.Length} resources but the layout declares {elements.Length}.", nameof(description));

        var setLayout = _layout.Handle;
        var allocateInfo = new DescriptorSetAllocateInfo
        {
            SType = StructureType.DescriptorSetAllocateInfo,
            DescriptorPool = _layout.Pool,
            DescriptorSetCount = 1,
            PSetLayouts = &setLayout,
        };

        DescriptorSet set;
        VulkanUtil.Check(context.Vk.AllocateDescriptorSets(context.Device, &allocateInfo, &set), "vkAllocateDescriptorSets");
        Handle = set;

        var writes = stackalloc WriteDescriptorSet[elements.Length];
        var bufferInfos = stackalloc DescriptorBufferInfo[elements.Length];
        var imageInfos = stackalloc DescriptorImageInfo[elements.Length];

        for (var i = 0; i < elements.Length; i++)
        {
            var kind = elements[i].Kind;
            var write = new WriteDescriptorSet
            {
                SType = StructureType.WriteDescriptorSet,
                DstSet = Handle,
                DstBinding = (uint)i,
                DstArrayElement = 0,
                DescriptorCount = 1,
                DescriptorType = VulkanFormats.MapResourceKind(kind),
            };

            switch (kind)
            {
                case ResourceKind.UniformBuffer:
                    bufferInfos[i] = new DescriptorBufferInfo
                    {
                        Buffer = ((VulkanBuffer)resources[i]).Handle,
                        Offset = 0,
                        Range = Vk.WholeSize,
                    };
                    write.PBufferInfo = &bufferInfos[i];
                    break;

                case ResourceKind.TextureReadOnly:
                    imageInfos[i] = new DescriptorImageInfo
                    {
                        ImageView = ((VulkanTexture)resources[i]).View,
                        ImageLayout = ImageLayout.ShaderReadOnlyOptimal,
                    };
                    write.PImageInfo = &imageInfos[i];
                    break;

                case ResourceKind.Sampler:
                    imageInfos[i] = new DescriptorImageInfo
                    {
                        Sampler = ((VulkanSampler)resources[i]).Handle,
                    };
                    write.PImageInfo = &imageInfos[i];
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(description), $"Unsupported resource kind {kind}.");
            }

            writes[i] = write;
        }

        context.Vk.UpdateDescriptorSets(context.Device, (uint)elements.Length, writes, 0, null);
    }

    public void Dispose()
    {
        var set = Handle;
        _context.Vk.FreeDescriptorSets(_context.Device, _layout.Pool, 1, &set);
    }
}
