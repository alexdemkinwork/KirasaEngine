using Silk.NET.Vulkan;

namespace KirasaEngine.MGL.Rendering.Backends.Vulkan;

/// <summary>A 2D <see cref="Image"/> + backing memory + default <see cref="ImageView"/>, mirroring GLTexture.</summary>
internal sealed unsafe class VulkanTexture : ITexture
{
    private readonly VulkanContext _context;

    public Image Handle { get; }
    public DeviceMemory Memory { get; }
    public ImageView View { get; }
    public Format VkFormat { get; }
    public ImageAspectFlags Aspect { get; }

    public uint Width { get; }
    public uint Height { get; }
    public TextureFormat Format { get; }
    public TextureUsage Usage { get; }

    /// <summary>
    /// Last layout this image is known to be in. Tracked (rather than re-derived) so readback can barrier
    /// from the right source layout without discarding the rendered contents.
    /// </summary>
    public ImageLayout CurrentLayout { get; set; } = ImageLayout.Undefined;

    public VulkanTexture(VulkanContext context, in TextureDescription description, ReadOnlySpan<byte> initialData)
    {
        _context = context;
        Width = description.Width;
        Height = description.Height;
        Format = description.Format;
        Usage = description.Usage;
        VkFormat = context.MapFormat(description.Format);

        var isDepth = VulkanFormats.IsDepthFormat(description.Format);
        Aspect = isDepth
            ? ImageAspectFlags.DepthBit | (VulkanFormats.HasStencil(VkFormat) ? ImageAspectFlags.StencilBit : 0)
            : ImageAspectFlags.ColorBit;

        var usageFlags = ImageUsageFlags.None;
        if (description.Usage.HasFlag(TextureUsage.Sampled)) usageFlags |= ImageUsageFlags.SampledBit;
        if (description.Usage.HasFlag(TextureUsage.RenderTarget))
            usageFlags |= ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.TransferSrcBit;
        if (description.Usage.HasFlag(TextureUsage.DepthStencil)) usageFlags |= ImageUsageFlags.DepthStencilAttachmentBit;
        if (!initialData.IsEmpty) usageFlags |= ImageUsageFlags.TransferDstBit;
        if (usageFlags == ImageUsageFlags.None) usageFlags = ImageUsageFlags.SampledBit;

        var imageInfo = new ImageCreateInfo
        {
            SType = StructureType.ImageCreateInfo,
            ImageType = ImageType.Type2D,
            Format = VkFormat,
            Extent = new Extent3D(Math.Max(Width, 1), Math.Max(Height, 1), 1),
            MipLevels = Math.Max(description.MipLevels, 1),
            ArrayLayers = 1,
            Samples = SampleCountFlags.Count1Bit,
            Tiling = ImageTiling.Optimal,
            Usage = usageFlags,
            SharingMode = SharingMode.Exclusive,
            InitialLayout = ImageLayout.Undefined,
        };

        Image image;
        VulkanUtil.Check(context.Vk.CreateImage(context.Device, &imageInfo, null, &image), "vkCreateImage");
        Handle = image;

        context.Vk.GetImageMemoryRequirements(context.Device, Handle, out var requirements);
        var allocateInfo = new MemoryAllocateInfo
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = requirements.Size,
            MemoryTypeIndex = context.FindMemoryType(requirements.MemoryTypeBits, MemoryPropertyFlags.DeviceLocalBit),
        };

        DeviceMemory memory;
        VulkanUtil.Check(context.Vk.AllocateMemory(context.Device, &allocateInfo, null, &memory), "vkAllocateMemory (image)");
        Memory = memory;
        VulkanUtil.Check(context.Vk.BindImageMemory(context.Device, Handle, Memory, 0), "vkBindImageMemory");

        var viewInfo = new ImageViewCreateInfo
        {
            SType = StructureType.ImageViewCreateInfo,
            Image = Handle,
            ViewType = ImageViewType.Type2D,
            Format = VkFormat,
            Components = new ComponentMapping(ComponentSwizzle.Identity, ComponentSwizzle.Identity, ComponentSwizzle.Identity, ComponentSwizzle.Identity),
            SubresourceRange = new ImageSubresourceRange(Aspect, 0, imageInfo.MipLevels, 0, 1),
        };

        ImageView view;
        VulkanUtil.Check(context.Vk.CreateImageView(context.Device, &viewInfo, null, &view), "vkCreateImageView");
        View = view;

        if (!initialData.IsEmpty) UploadInitialData(initialData);
    }

    /// <summary>Staging buffer -> UNDEFINED -> TRANSFER_DST_OPTIMAL -> copy -> SHADER_READ_ONLY_OPTIMAL, submitted synchronously.</summary>
    private void UploadInitialData(ReadOnlySpan<byte> data)
    {
        var (staging, stagingMemory) = _context.CreateRawBuffer(
            (ulong)data.Length,
            BufferUsageFlags.TransferSrcBit,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);

        try
        {
            void* mapped;
            VulkanUtil.Check(_context.Vk.MapMemory(_context.Device, stagingMemory, 0, (ulong)data.Length, 0, &mapped), "vkMapMemory (texture staging)");
            data.CopyTo(new Span<byte>(mapped, data.Length));
            _context.Vk.UnmapMemory(_context.Device, stagingMemory);

            var cmd = _context.BeginOneTimeCommands();

            _context.TransitionImageLayout(cmd, Handle, Aspect, ImageLayout.Undefined, ImageLayout.TransferDstOptimal);

            var region = new BufferImageCopy
            {
                BufferOffset = 0,
                // 0/0 means "tightly packed to the image extent" per spec - the source span always is.
                BufferRowLength = 0,
                BufferImageHeight = 0,
                ImageSubresource = new ImageSubresourceLayers(Aspect, 0, 0, 1),
                ImageOffset = new Offset3D(0, 0, 0),
                ImageExtent = new Extent3D(Width, Height, 1),
            };
            _context.Vk.CmdCopyBufferToImage(cmd, staging, Handle, ImageLayout.TransferDstOptimal, 1, &region);

            _context.TransitionImageLayout(cmd, Handle, Aspect, ImageLayout.TransferDstOptimal, ImageLayout.ShaderReadOnlyOptimal);

            _context.EndOneTimeCommands(cmd);
            CurrentLayout = ImageLayout.ShaderReadOnlyOptimal;
        }
        finally
        {
            _context.Vk.DestroyBuffer(_context.Device, staging, null);
            _context.Vk.FreeMemory(_context.Device, stagingMemory, null);
        }
    }

    public void Dispose()
    {
        var vk = _context.Vk;
        vk.DestroyImageView(_context.Device, View, null);
        vk.DestroyImage(_context.Device, Handle, null);
        vk.FreeMemory(_context.Device, Memory, null);
    }
}
