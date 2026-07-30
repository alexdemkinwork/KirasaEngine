using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using VkPipeline = Silk.NET.Vulkan.Pipeline;

using KirasaEngine.MGL.Rendering;
using VulkanRenderPass = Silk.NET.Vulkan.RenderPass;

namespace KirasaEngine.MGL.Rendering.Backends.Vulkan;

/// <summary>
/// A monolithic <see cref="VkPipeline"/> baking everything <c>PipelineDescription</c> describes, plus the
/// <see cref="PipelineLayout"/> and a private <see cref="RenderPass"/>.
/// </summary>
/// <remarks>
/// The render pass here is built from <c>ColorFormat</c>/<c>DepthFormat</c> by the same
/// <see cref="VulkanContext.CreateRenderPass"/> helper <see cref="VulkanRenderTarget"/> uses, so the two
/// handles are always render-pass *compatible* (identical attachment formats and sample counts), which is
/// all Vulkan requires between a framebuffer and the pipelines drawing into it.
/// Viewport and scissor are dynamic state: the target size is not known when the pipeline is created, and
/// the Vulkan NDC-Y flip is applied as a negative-height viewport in <see cref="VulkanCommandList.SetViewport"/>.
/// </remarks>
internal sealed unsafe class VulkanPipeline : IPipeline
{
    private readonly VulkanContext _context;

    public PipelineDescription Description { get; }
    public VulkanShaderSet ShaderSet { get; }
    public PipelineLayout Layout { get; }
    public VulkanRenderPass RenderPass { get; }
    public VkPipeline Handle { get; }

    public VulkanPipeline(VulkanContext context, PipelineDescription description)
    {
        _context = context;
        Description = description;
        ShaderSet = (VulkanShaderSet)description.ShaderSet;

        var resourceLayout = (VulkanResourceLayout)description.ResourceLayout;
        var setLayout = resourceLayout.Handle;

        var layoutInfo = new PipelineLayoutCreateInfo
        {
            SType = StructureType.PipelineLayoutCreateInfo,
            SetLayoutCount = 1,
            PSetLayouts = &setLayout,
        };

        PipelineLayout pipelineLayout;
        VulkanUtil.Check(context.Vk.CreatePipelineLayout(context.Device, &layoutInfo, null, &pipelineLayout), "vkCreatePipelineLayout");
        Layout = pipelineLayout;

        RenderPass = context.CreateRenderPass(description.ColorFormat, description.DepthFormat);
        // Используем полное имя для избежания конфликта с RenderGraph.RenderPass

        var entryPoint = SilkMarshal.StringToPtr("main");
        try
        {
            Handle = CreatePipeline(context, description, (byte*)entryPoint);
        }
        finally
        {
            SilkMarshal.Free(entryPoint);
        }
    }

    private VkPipeline CreatePipeline(VulkanContext context, PipelineDescription description, byte* entryPoint)
    {
        var stages = stackalloc PipelineShaderStageCreateInfo[2];
        stages[0] = new PipelineShaderStageCreateInfo
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.VertexBit,
            Module = ShaderSet.VertexModule,
            PName = entryPoint,
        };
        stages[1] = new PipelineShaderStageCreateInfo
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.FragmentBit,
            Module = ShaderSet.FragmentModule,
            PName = entryPoint,
        };

        var layouts = ShaderSet.VertexLayouts;
        var bindings = new VertexInputBindingDescription[layouts.Length];
        var attributeList = new List<VertexInputAttributeDescription>();

        for (var slot = 0; slot < layouts.Length; slot++)
        {
            var layout = layouts[slot];
            bindings[slot] = new VertexInputBindingDescription
            {
                Binding = (uint)slot,
                Stride = layout.Stride,
                InputRate = VulkanFormats.MapInputRate(layout.InputRate),
            };

            foreach (var element in layout.Elements)
            {
                attributeList.Add(new VertexInputAttributeDescription
                {
                    Location = element.Location,
                    Binding = (uint)slot,
                    Format = VulkanFormats.MapVertexElement(element.Format),
                    Offset = element.Offset,
                });
            }
        }

        var attributes = attributeList.ToArray();
        var dynamicStates = stackalloc DynamicState[2];
        dynamicStates[0] = DynamicState.Viewport;
        dynamicStates[1] = DynamicState.Scissor;

        fixed (VertexInputBindingDescription* pBindings = bindings)
        fixed (VertexInputAttributeDescription* pAttributes = attributes)
        {
            var vertexInput = new PipelineVertexInputStateCreateInfo
            {
                SType = StructureType.PipelineVertexInputStateCreateInfo,
                VertexBindingDescriptionCount = (uint)bindings.Length,
                PVertexBindingDescriptions = pBindings,
                VertexAttributeDescriptionCount = (uint)attributes.Length,
                PVertexAttributeDescriptions = pAttributes,
            };

            var inputAssembly = new PipelineInputAssemblyStateCreateInfo
            {
                SType = StructureType.PipelineInputAssemblyStateCreateInfo,
                Topology = VulkanFormats.MapTopology(description.Topology),
                PrimitiveRestartEnable = false,
            };

            // Counts must still be 1 even though the values themselves come from vkCmdSetViewport/Scissor.
            var viewportState = new PipelineViewportStateCreateInfo
            {
                SType = StructureType.PipelineViewportStateCreateInfo,
                ViewportCount = 1,
                ScissorCount = 1,
            };

            var rasterization = new PipelineRasterizationStateCreateInfo
            {
                SType = StructureType.PipelineRasterizationStateCreateInfo,
                DepthClampEnable = false,
                RasterizerDiscardEnable = false,
                PolygonMode = VulkanFormats.MapFillMode(description.FillMode),
                CullMode = VulkanFormats.MapCullMode(description.CullMode),
                FrontFace = VulkanFormats.MapFrontFace(description.FrontFace),
                DepthBiasEnable = false,
                LineWidth = 1f,
            };

            var multisample = new PipelineMultisampleStateCreateInfo
            {
                SType = StructureType.PipelineMultisampleStateCreateInfo,
                RasterizationSamples = SampleCountFlags.Count1Bit,
                SampleShadingEnable = false,
                MinSampleShading = 1f,
            };

            var depthStencil = new PipelineDepthStencilStateCreateInfo
            {
                SType = StructureType.PipelineDepthStencilStateCreateInfo,
                DepthTestEnable = description.DepthTestEnabled,
                DepthWriteEnable = description.DepthWriteEnabled,
                DepthCompareOp = VulkanFormats.MapCompare(description.DepthCompare),
                DepthBoundsTestEnable = false,
                StencilTestEnable = false,
                MinDepthBounds = 0f,
                MaxDepthBounds = 1f,
            };

            var blendAttachment = MapBlend(description.Blend);
            var colorBlend = new PipelineColorBlendStateCreateInfo
            {
                SType = StructureType.PipelineColorBlendStateCreateInfo,
                LogicOpEnable = false,
                LogicOp = LogicOp.Copy,
                AttachmentCount = 1,
                PAttachments = &blendAttachment,
            };

            var dynamicState = new PipelineDynamicStateCreateInfo
            {
                SType = StructureType.PipelineDynamicStateCreateInfo,
                DynamicStateCount = 2,
                PDynamicStates = dynamicStates,
            };

            var createInfo = new GraphicsPipelineCreateInfo
            {
                SType = StructureType.GraphicsPipelineCreateInfo,
                StageCount = 2,
                PStages = stages,
                PVertexInputState = &vertexInput,
                PInputAssemblyState = &inputAssembly,
                PViewportState = &viewportState,
                PRasterizationState = &rasterization,
                PMultisampleState = &multisample,
                PDepthStencilState = description.DepthFormat is null ? null : &depthStencil,
                PColorBlendState = &colorBlend,
                PDynamicState = &dynamicState,
                Layout = Layout,
                RenderPass = RenderPass,
                Subpass = 0,
            };

            VkPipeline pipeline;
            VulkanUtil.Check(
                context.Vk.CreateGraphicsPipelines(context.Device, default, 1, &createInfo, null, &pipeline),
                "vkCreateGraphicsPipelines");
            return pipeline;
        }
    }

    private static PipelineColorBlendAttachmentState MapBlend(BlendMode blend)
    {
        const ColorComponentFlags writeAll =
            ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit | ColorComponentFlags.ABit;

        return blend switch
        {
            BlendMode.Opaque => new PipelineColorBlendAttachmentState
            {
                BlendEnable = false,
                ColorWriteMask = writeAll,
            },
            BlendMode.AlphaBlend => new PipelineColorBlendAttachmentState
            {
                BlendEnable = true,
                SrcColorBlendFactor = BlendFactor.SrcAlpha,
                DstColorBlendFactor = BlendFactor.OneMinusSrcAlpha,
                ColorBlendOp = BlendOp.Add,
                SrcAlphaBlendFactor = BlendFactor.One,
                DstAlphaBlendFactor = BlendFactor.OneMinusSrcAlpha,
                AlphaBlendOp = BlendOp.Add,
                ColorWriteMask = writeAll,
            },
            BlendMode.Additive => new PipelineColorBlendAttachmentState
            {
                BlendEnable = true,
                SrcColorBlendFactor = BlendFactor.SrcAlpha,
                DstColorBlendFactor = BlendFactor.One,
                ColorBlendOp = BlendOp.Add,
                SrcAlphaBlendFactor = BlendFactor.One,
                DstAlphaBlendFactor = BlendFactor.One,
                AlphaBlendOp = BlendOp.Add,
                ColorWriteMask = writeAll,
            },
            _ => throw new ArgumentOutOfRangeException(nameof(blend)),
        };
    }

    public void Dispose()
    {
        var vk = _context.Vk;
        vk.DestroyPipeline(_context.Device, Handle, null);
        vk.DestroyRenderPass(_context.Device, RenderPass, null);
        vk.DestroyPipelineLayout(_context.Device, Layout, null);
        ShaderSet.Dispose();
    }
}
