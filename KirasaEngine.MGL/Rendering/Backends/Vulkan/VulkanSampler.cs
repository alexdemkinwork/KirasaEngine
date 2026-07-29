using Silk.NET.Vulkan;
using VkSampler = Silk.NET.Vulkan.Sampler;

namespace KirasaEngine.MGL.Rendering.Backends.Vulkan;

internal sealed unsafe class VulkanSampler : ISampler
{
    private readonly VulkanContext _context;

    public VkSampler Handle { get; }

    public VulkanSampler(VulkanContext context, in SamplerDescription description)
    {
        _context = context;

        var address = VulkanFormats.MapAddressMode(description.AddressMode);
        var createInfo = new SamplerCreateInfo
        {
            SType = StructureType.SamplerCreateInfo,
            MagFilter = VulkanFormats.MapFilter(description.Filter),
            MinFilter = VulkanFormats.MapFilter(description.Filter),
            MipmapMode = VulkanFormats.MapMipmapMode(description.Filter),
            AddressModeU = address,
            AddressModeV = address,
            AddressModeW = address,
            MipLodBias = 0f,
            AnisotropyEnable = false,
            MaxAnisotropy = 1f,
            CompareEnable = false,
            CompareOp = CompareOp.Always,
            MinLod = 0f,
            MaxLod = Vk.LodClampNone,
            BorderColor = BorderColor.FloatOpaqueBlack,
            UnnormalizedCoordinates = false,
        };

        VkSampler sampler;
        VulkanUtil.Check(context.Vk.CreateSampler(context.Device, &createInfo, null, &sampler), "vkCreateSampler");
        Handle = sampler;
    }

    public void Dispose() => _context.Vk.DestroySampler(_context.Device, Handle, null);
}
