using Silk.NET.Vulkan;

namespace KirasaEngine.MGL.Rendering.Backends.Vulkan;

/// <summary>
/// Color (+ optional depth) <see cref="VulkanTexture"/> pair, together with the <see cref="RenderPass"/> and
/// <see cref="Framebuffer"/> the command list begins. The render pass is derived purely from the attachment
/// formats via <see cref="VulkanContext.CreateRenderPass"/>, so it is compatible with the (distinct) render
/// pass any <see cref="VulkanPipeline"/> with the same formats was created against.
/// </summary>
internal sealed unsafe class VulkanRenderTarget : IRenderTarget
{
    private readonly VulkanContext _context;

    public uint Width { get; }
    public uint Height { get; }
    public TextureFormat ColorFormat { get; }
    public ITexture ColorTexture { get; }
    public ITexture? DepthTexture { get; }

    public RenderPass RenderPass { get; }
    public Framebuffer Framebuffer { get; }

    public VulkanTexture Color => (VulkanTexture)ColorTexture;
    public VulkanTexture? Depth => (VulkanTexture?)DepthTexture;

    public VulkanRenderTarget(VulkanContext context, in RenderTargetDescription description)
    {
        _context = context;
        Width = description.Width;
        Height = description.Height;
        ColorFormat = description.ColorFormat;

        ColorTexture = new VulkanTexture(
            context,
            new TextureDescription(Width, Height, ColorFormat, TextureUsage.RenderTarget | TextureUsage.Sampled),
            default);

        if (description.DepthFormat is { } depthFormat)
        {
            DepthTexture = new VulkanTexture(
                context,
                new TextureDescription(Width, Height, depthFormat, TextureUsage.DepthStencil),
                default);
        }

        RenderPass = context.CreateRenderPass(description.ColorFormat, description.DepthFormat);

        var attachments = stackalloc ImageView[2];
        attachments[0] = Color.View;
        uint attachmentCount = 1;
        if (Depth is { } depth)
        {
            attachments[1] = depth.View;
            attachmentCount = 2;
        }

        var framebufferInfo = new FramebufferCreateInfo
        {
            SType = StructureType.FramebufferCreateInfo,
            RenderPass = RenderPass,
            AttachmentCount = attachmentCount,
            PAttachments = attachments,
            Width = Width,
            Height = Height,
            Layers = 1,
        };

        Framebuffer framebuffer;
        VulkanUtil.Check(context.Vk.CreateFramebuffer(context.Device, &framebufferInfo, null, &framebuffer), "vkCreateFramebuffer");
        Framebuffer = framebuffer;
    }

    public void Dispose()
    {
        _context.Vk.DestroyFramebuffer(_context.Device, Framebuffer, null);
        _context.Vk.DestroyRenderPass(_context.Device, RenderPass, null);
        ColorTexture.Dispose();
        DepthTexture?.Dispose();
    }
}
