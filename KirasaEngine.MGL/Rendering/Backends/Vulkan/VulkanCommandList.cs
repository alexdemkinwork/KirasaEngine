using Silk.NET.Vulkan;
using AbsViewport = KirasaEngine.MGL.Rendering.Abstractions.Structs.Viewport;

namespace KirasaEngine.MGL.Rendering.Backends.Vulkan;

/// <summary>
/// Records into a primary <see cref="CommandBuffer"/> allocated from the device's RESET_COMMAND_BUFFER pool.
/// </summary>
/// <remarks>
/// The render pass is begun eagerly in <see cref="SetRenderTarget"/> with LOAD_OP_DONT_CARE, and
/// <see cref="ClearColor"/>/<see cref="ClearDepthStencil"/> are serviced by <c>vkCmdClearAttachments</c>
/// inside it. That maps ICommandList's "set target, then clear, then draw" order onto Vulkan directly,
/// without having to guess at clear values before the render pass starts.
/// </remarks>
internal sealed unsafe class VulkanCommandList : ICommandList
{
    private readonly VulkanContext _context;
    private readonly Vk _vk;

    private VulkanRenderTarget? _target;
    private VulkanPipeline? _pipeline;
    private bool _renderPassActive;
    private bool _recording;

    public CommandBuffer CommandBuffer { get; }

    public VulkanCommandList(VulkanContext context)
    {
        _context = context;
        _vk = context.Vk;

        var allocateInfo = new CommandBufferAllocateInfo
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = context.CommandPool,
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = 1,
        };

        CommandBuffer commandBuffer;
        VulkanUtil.Check(_vk.AllocateCommandBuffers(context.Device, &allocateInfo, &commandBuffer), "vkAllocateCommandBuffers");
        CommandBuffer = commandBuffer;
    }

    public void Begin()
    {
        VulkanUtil.Check(_vk.ResetCommandBuffer(CommandBuffer, 0), "vkResetCommandBuffer");

        var beginInfo = new CommandBufferBeginInfo
        {
            SType = StructureType.CommandBufferBeginInfo,
            Flags = CommandBufferUsageFlags.OneTimeSubmitBit,
        };
        VulkanUtil.Check(_vk.BeginCommandBuffer(CommandBuffer, &beginInfo), "vkBeginCommandBuffer");

        _recording = true;
        _renderPassActive = false;
        _target = null;
        _pipeline = null;
    }

    public void End()
    {
        EndRenderPassIfActive();
        VulkanUtil.Check(_vk.EndCommandBuffer(CommandBuffer), "vkEndCommandBuffer");
        _recording = false;
    }

    public void SetRenderTarget(IRenderTarget? target)
    {
        if (target is null)
            throw new NotSupportedException("The Vulkan backend is offscreen-only; there is no swap chain backbuffer to target (pass an IRenderTarget).");

        EndRenderPassIfActive();
        _target = (VulkanRenderTarget)target;

        var beginInfo = new RenderPassBeginInfo
        {
            SType = StructureType.RenderPassBeginInfo,
            RenderPass = _target.RenderPass,
            Framebuffer = _target.Framebuffer,
            RenderArea = new Rect2D(new Offset2D(0, 0), new Extent2D(_target.Width, _target.Height)),
            ClearValueCount = 0,
        };

        _vk.CmdBeginRenderPass(CommandBuffer, &beginInfo, SubpassContents.Inline);
        _renderPassActive = true;
    }

    public void ClearColor(Vector4 color)
    {
        var target = RequireTarget();

        var attachment = new ClearAttachment
        {
            AspectMask = ImageAspectFlags.ColorBit,
            ColorAttachment = 0,
            ClearValue = new ClearValue
            {
                Color = new ClearColorValue { Float32_0 = color.X, Float32_1 = color.Y, Float32_2 = color.Z, Float32_3 = color.W },
            },
        };
        var rect = FullTargetClearRect(target);

        _vk.CmdClearAttachments(CommandBuffer, 1, &attachment, 1, &rect);
    }

    public void ClearDepthStencil(float depth = 1f, byte stencil = 0)
    {
        var target = RequireTarget();
        if (target.Depth is not { } depthTexture) return;

        var attachment = new ClearAttachment
        {
            AspectMask = depthTexture.Aspect,
            ClearValue = new ClearValue
            {
                DepthStencil = new ClearDepthStencilValue { Depth = depth, Stencil = stencil },
            },
        };
        var rect = FullTargetClearRect(target);

        _vk.CmdClearAttachments(CommandBuffer, 1, &attachment, 1, &rect);
    }

    /// <summary>
    /// Applies the Vulkan NDC-Y fix: a negative-height viewport (y flipped to the bottom edge, height
    /// negated) makes clip-space +Y point up in the framebuffer, exactly like OpenGL/D3D. Requires Vulkan
    /// 1.1 core (or VK_KHR_maintenance1), both of which the device explicitly asks for. The camera's
    /// projection matrix stays untouched and backend-agnostic. A matching full-viewport scissor is issued
    /// here too, because scissor is dynamic state and SceneRenderer never calls SetScissor.
    /// </summary>
    public void SetViewport(in AbsViewport viewport)
    {
        var vkViewport = new Silk.NET.Vulkan.Viewport
        {
            X = viewport.X,
            Y = viewport.Y + viewport.Height,
            Width = viewport.Width,
            Height = -viewport.Height,
            MinDepth = viewport.MinDepth,
            MaxDepth = viewport.MaxDepth,
        };
        _vk.CmdSetViewport(CommandBuffer, 0, 1, &vkViewport);

        var scissor = new Rect2D(
            new Offset2D((int)viewport.X, (int)viewport.Y),
            new Extent2D((uint)Math.Max(viewport.Width, 0), (uint)Math.Max(viewport.Height, 0)));
        _vk.CmdSetScissor(CommandBuffer, 0, 1, &scissor);
    }

    public void SetScissor(in RectI rect)
    {
        var scissor = new Rect2D(
            new Offset2D(rect.X, rect.Y),
            new Extent2D((uint)Math.Max(rect.Width, 0), (uint)Math.Max(rect.Height, 0)));
        _vk.CmdSetScissor(CommandBuffer, 0, 1, &scissor);
    }

    public void SetPipeline(IPipeline pipeline)
    {
        _pipeline = (VulkanPipeline)pipeline;
        _vk.CmdBindPipeline(CommandBuffer, PipelineBindPoint.Graphics, _pipeline.Handle);
    }

    public void SetVertexBuffer(uint slot, IBuffer buffer, uint offset = 0)
    {
        var handle = ((VulkanBuffer)buffer).Handle;
        ulong bufferOffset = offset;
        _vk.CmdBindVertexBuffers(CommandBuffer, slot, 1, &handle, &bufferOffset);
    }

    public void SetIndexBuffer(IBuffer buffer, IndexFormat format, uint offset = 0) =>
        _vk.CmdBindIndexBuffer(CommandBuffer, ((VulkanBuffer)buffer).Handle, offset, VulkanFormats.MapIndexFormat(format));

    public void SetResourceSet(uint slot, IResourceSet resourceSet)
    {
        if (_pipeline is null) throw new InvalidOperationException("SetPipeline must be called before SetResourceSet.");

        var set = ((VulkanResourceSet)resourceSet).Handle;
        _vk.CmdBindDescriptorSets(CommandBuffer, PipelineBindPoint.Graphics, _pipeline.Layout, slot, 1, &set, 0, null);
    }

    public void UpdateBuffer(IBuffer buffer, ReadOnlySpan<byte> data, uint destinationOffsetBytes = 0) =>
        ((VulkanBuffer)buffer).SetData(data, destinationOffsetBytes);

    public void DrawIndexed(uint indexCount, uint instanceCount = 1, uint firstIndex = 0, int vertexOffset = 0, uint firstInstance = 0)
    {
        if (_pipeline is null) throw new InvalidOperationException("SetPipeline must be called before DrawIndexed.");
        _vk.CmdDrawIndexed(CommandBuffer, indexCount, instanceCount, firstIndex, vertexOffset, firstInstance);
    }

    private VulkanRenderTarget RequireTarget() =>
        _target ?? throw new InvalidOperationException("SetRenderTarget must be called before clearing.");

    private static ClearRect FullTargetClearRect(VulkanRenderTarget target) => new()
    {
        BaseArrayLayer = 0,
        LayerCount = 1,
        Rect = new Rect2D(new Offset2D(0, 0), new Extent2D(target.Width, target.Height)),
    };

    private void EndRenderPassIfActive()
    {
        if (!_renderPassActive) return;

        _vk.CmdEndRenderPass(CommandBuffer);
        _renderPassActive = false;

        // Mirrors the render pass's finalLayouts so ReadRenderTarget barriers from the correct source layout.
        if (_target is { } target)
        {
            target.Color.CurrentLayout = ImageLayout.ColorAttachmentOptimal;
            if (target.Depth is { } depth) depth.CurrentLayout = ImageLayout.DepthStencilAttachmentOptimal;
        }
    }

    public void Dispose()
    {
        if (_recording)
        {
            // Nothing may be submitted from a half-recorded buffer; close it so vkFreeCommandBuffers is legal.
            EndRenderPassIfActive();
            _vk.EndCommandBuffer(CommandBuffer);
            _recording = false;
        }

        var commandBuffer = CommandBuffer;
        _vk.FreeCommandBuffers(_context.Device, _context.CommandPool, 1, &commandBuffer);
    }
}
