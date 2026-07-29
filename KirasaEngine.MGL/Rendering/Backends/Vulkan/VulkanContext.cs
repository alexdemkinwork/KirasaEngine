using Silk.NET.Vulkan;
using VkBuffer = Silk.NET.Vulkan.Buffer;

namespace KirasaEngine.MGL.Rendering.Backends.Vulkan;

/// <summary>
/// The handful of device-wide Vulkan objects every resource type needs, plus the small allocation and
/// one-shot-submit helpers they share. Passed around exactly like <c>GL</c> is in the OpenGL backend.
/// </summary>
/// <remarks>
/// Memory management is deliberately naive — one <see cref="DeviceMemory"/> allocation per buffer/image.
/// This project's resource set is tiny and fixed, and correctness/readability beat allocator throughput
/// here (same rationale as the fully synchronous submit model on <see cref="IGraphicsDevice.Submit"/>).
/// </remarks>
internal sealed unsafe class VulkanContext(
    Vk vk,
    Instance instance,
    PhysicalDevice physicalDevice,
    Device device,
    Queue graphicsQueue,
    uint graphicsQueueFamily,
    CommandPool commandPool,
    Fence submitFence,
    Format depthStencilFormat)
{
    public Vk Vk { get; } = vk;
    public Instance Instance { get; } = instance;
    public PhysicalDevice PhysicalDevice { get; } = physicalDevice;
    public Device Device { get; } = device;
    public Queue GraphicsQueue { get; } = graphicsQueue;
    public uint GraphicsQueueFamily { get; } = graphicsQueueFamily;
    public CommandPool CommandPool { get; } = commandPool;
    public Fence SubmitFence { get; } = submitFence;

    /// <summary>Physical-device-supported stand-in for <see cref="TextureFormat.Depth24Stencil8"/>.</summary>
    public Format DepthStencilFormat { get; } = depthStencilFormat;

    /// <summary>Like <see cref="VulkanFormats.MapTexture"/>, but substitutes a supported depth-stencil format.</summary>
    public Format MapFormat(TextureFormat format) =>
        format == TextureFormat.Depth24Stencil8 ? DepthStencilFormat : VulkanFormats.MapTexture(format);

    public uint FindMemoryType(uint typeBits, MemoryPropertyFlags required)
    {
        Vk.GetPhysicalDeviceMemoryProperties(PhysicalDevice, out var properties);
        for (var i = 0; i < properties.MemoryTypeCount; i++)
        {
            if ((typeBits & (1u << i)) == 0) continue;
            if ((properties.MemoryTypes[i].PropertyFlags & required) == required) return (uint)i;
        }

        throw new InvalidOperationException($"No Vulkan memory type satisfies {required} (typeBits 0x{typeBits:X}).");
    }

    public (VkBuffer Buffer, DeviceMemory Memory) CreateRawBuffer(ulong sizeInBytes, BufferUsageFlags usage, MemoryPropertyFlags memoryProperties)
    {
        var createInfo = new BufferCreateInfo
        {
            SType = StructureType.BufferCreateInfo,
            Size = Math.Max(sizeInBytes, 1),
            Usage = usage,
            SharingMode = SharingMode.Exclusive,
        };

        VkBuffer buffer;
        VulkanUtil.Check(Vk.CreateBuffer(Device, &createInfo, null, &buffer), "vkCreateBuffer");

        Vk.GetBufferMemoryRequirements(Device, buffer, out var requirements);
        var allocateInfo = new MemoryAllocateInfo
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = requirements.Size,
            MemoryTypeIndex = FindMemoryType(requirements.MemoryTypeBits, memoryProperties),
        };

        DeviceMemory memory;
        VulkanUtil.Check(Vk.AllocateMemory(Device, &allocateInfo, null, &memory), "vkAllocateMemory (buffer)");
        VulkanUtil.Check(Vk.BindBufferMemory(Device, buffer, memory, 0), "vkBindBufferMemory");

        return (buffer, memory);
    }

    /// <summary>Allocates a primary command buffer and puts it in the recording state for a one-shot upload/copy.</summary>
    public CommandBuffer BeginOneTimeCommands()
    {
        var allocateInfo = new CommandBufferAllocateInfo
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = CommandPool,
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = 1,
        };

        CommandBuffer commandBuffer;
        VulkanUtil.Check(Vk.AllocateCommandBuffers(Device, &allocateInfo, &commandBuffer), "vkAllocateCommandBuffers (one-time)");

        var beginInfo = new CommandBufferBeginInfo
        {
            SType = StructureType.CommandBufferBeginInfo,
            Flags = CommandBufferUsageFlags.OneTimeSubmitBit,
        };
        VulkanUtil.Check(Vk.BeginCommandBuffer(commandBuffer, &beginInfo), "vkBeginCommandBuffer (one-time)");

        return commandBuffer;
    }

    /// <summary>Ends, submits and fully waits for a buffer from <see cref="BeginOneTimeCommands"/>, then frees it.</summary>
    public void EndOneTimeCommands(CommandBuffer commandBuffer)
    {
        VulkanUtil.Check(Vk.EndCommandBuffer(commandBuffer), "vkEndCommandBuffer (one-time)");
        SubmitAndWait(commandBuffer);
        Vk.FreeCommandBuffers(Device, CommandPool, 1, &commandBuffer);
    }

    /// <summary>The project-wide synchronous submit: queue the work, then block until the GPU is finished with it.</summary>
    public void SubmitAndWait(CommandBuffer commandBuffer)
    {
        var fence = SubmitFence;
        VulkanUtil.Check(Vk.ResetFences(Device, 1, &fence), "vkResetFences");

        var submitInfo = new SubmitInfo
        {
            SType = StructureType.SubmitInfo,
            CommandBufferCount = 1,
            PCommandBuffers = &commandBuffer,
        };
        VulkanUtil.Check(Vk.QueueSubmit(GraphicsQueue, 1, &submitInfo, fence), "vkQueueSubmit");
        VulkanUtil.Check(Vk.WaitForFences(Device, 1, &fence, true, ulong.MaxValue), "vkWaitForFences");
    }

    /// <summary>Records a layout transition with conservative (but correct) stage/access masks for this backend's usage.</summary>
    public void TransitionImageLayout(CommandBuffer commandBuffer, Image image, ImageAspectFlags aspect, ImageLayout oldLayout, ImageLayout newLayout)
    {
        var barrier = new ImageMemoryBarrier
        {
            SType = StructureType.ImageMemoryBarrier,
            OldLayout = oldLayout,
            NewLayout = newLayout,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Image = image,
            SubresourceRange = new ImageSubresourceRange(aspect, 0, 1, 0, 1),
            SrcAccessMask = AccessFor(oldLayout),
            DstAccessMask = AccessFor(newLayout),
        };

        Vk.CmdPipelineBarrier(
            commandBuffer,
            StageFor(oldLayout),
            StageFor(newLayout),
            0, 0, null, 0, null, 1, &barrier);
    }

    private static AccessFlags AccessFor(ImageLayout layout) => layout switch
    {
        ImageLayout.Undefined or ImageLayout.Preinitialized => AccessFlags.None,
        ImageLayout.TransferDstOptimal => AccessFlags.TransferWriteBit,
        ImageLayout.TransferSrcOptimal => AccessFlags.TransferReadBit,
        ImageLayout.ShaderReadOnlyOptimal => AccessFlags.ShaderReadBit,
        ImageLayout.ColorAttachmentOptimal => AccessFlags.ColorAttachmentWriteBit | AccessFlags.ColorAttachmentReadBit,
        ImageLayout.DepthStencilAttachmentOptimal => AccessFlags.DepthStencilAttachmentWriteBit | AccessFlags.DepthStencilAttachmentReadBit,
        _ => AccessFlags.MemoryReadBit | AccessFlags.MemoryWriteBit,
    };

    private static PipelineStageFlags StageFor(ImageLayout layout) => layout switch
    {
        ImageLayout.Undefined or ImageLayout.Preinitialized => PipelineStageFlags.TopOfPipeBit,
        ImageLayout.TransferDstOptimal or ImageLayout.TransferSrcOptimal => PipelineStageFlags.TransferBit,
        ImageLayout.ShaderReadOnlyOptimal => PipelineStageFlags.FragmentShaderBit,
        ImageLayout.ColorAttachmentOptimal => PipelineStageFlags.ColorAttachmentOutputBit,
        ImageLayout.DepthStencilAttachmentOptimal => PipelineStageFlags.EarlyFragmentTestsBit | PipelineStageFlags.LateFragmentTestsBit,
        _ => PipelineStageFlags.AllCommandsBit,
    };

    /// <summary>
    /// Builds the render pass shared by <see cref="VulkanRenderTarget"/> (for its framebuffer) and
    /// <see cref="VulkanPipeline"/>. Both derive it purely from the attachment formats, so the two objects
    /// are always render-pass *compatible* even though they are distinct <see cref="RenderPass"/> handles.
    /// LoadOp is DontCare because clears are issued with vkCmdClearAttachments once the render pass is
    /// already open (ICommandList exposes ClearColor/ClearDepthStencil after SetRenderTarget).
    /// </summary>
    public RenderPass CreateRenderPass(TextureFormat colorFormat, TextureFormat? depthFormat)
    {
        var attachments = stackalloc AttachmentDescription[2];
        attachments[0] = new AttachmentDescription
        {
            Format = MapFormat(colorFormat),
            Samples = SampleCountFlags.Count1Bit,
            LoadOp = AttachmentLoadOp.DontCare,
            StoreOp = AttachmentStoreOp.Store,
            StencilLoadOp = AttachmentLoadOp.DontCare,
            StencilStoreOp = AttachmentStoreOp.DontCare,
            InitialLayout = ImageLayout.Undefined,
            FinalLayout = ImageLayout.ColorAttachmentOptimal,
        };

        var colorReference = new AttachmentReference(0, ImageLayout.ColorAttachmentOptimal);
        var depthReference = new AttachmentReference(1, ImageLayout.DepthStencilAttachmentOptimal);

        var subpass = new SubpassDescription
        {
            PipelineBindPoint = PipelineBindPoint.Graphics,
            ColorAttachmentCount = 1,
            PColorAttachments = &colorReference,
        };

        uint attachmentCount = 1;
        if (depthFormat is { } depth)
        {
            attachments[1] = new AttachmentDescription
            {
                Format = MapFormat(depth),
                Samples = SampleCountFlags.Count1Bit,
                LoadOp = AttachmentLoadOp.DontCare,
                StoreOp = AttachmentStoreOp.DontCare,
                StencilLoadOp = AttachmentLoadOp.DontCare,
                StencilStoreOp = AttachmentStoreOp.DontCare,
                InitialLayout = ImageLayout.Undefined,
                FinalLayout = ImageLayout.DepthStencilAttachmentOptimal,
            };
            subpass.PDepthStencilAttachment = &depthReference;
            attachmentCount = 2;
        }

        var dependencies = stackalloc SubpassDependency[2];
        dependencies[0] = new SubpassDependency
        {
            SrcSubpass = Vk.SubpassExternal,
            DstSubpass = 0,
            SrcStageMask = PipelineStageFlags.TopOfPipeBit,
            DstStageMask = PipelineStageFlags.ColorAttachmentOutputBit | PipelineStageFlags.EarlyFragmentTestsBit,
            SrcAccessMask = AccessFlags.None,
            DstAccessMask = AccessFlags.ColorAttachmentWriteBit | AccessFlags.DepthStencilAttachmentWriteBit,
        };
        // Makes the rendered color image visible to the vkCmdCopyImageToBuffer readback that follows.
        dependencies[1] = new SubpassDependency
        {
            SrcSubpass = 0,
            DstSubpass = Vk.SubpassExternal,
            SrcStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
            DstStageMask = PipelineStageFlags.TransferBit,
            SrcAccessMask = AccessFlags.ColorAttachmentWriteBit,
            DstAccessMask = AccessFlags.TransferReadBit,
        };

        var createInfo = new RenderPassCreateInfo
        {
            SType = StructureType.RenderPassCreateInfo,
            AttachmentCount = attachmentCount,
            PAttachments = attachments,
            SubpassCount = 1,
            PSubpasses = &subpass,
            DependencyCount = 2,
            PDependencies = dependencies,
        };

        RenderPass renderPass;
        VulkanUtil.Check(Vk.CreateRenderPass(Device, &createInfo, null, &renderPass), "vkCreateRenderPass");
        return renderPass;
    }
}
