using System;
using System.Numerics;
using System.Runtime.InteropServices;

using KirasaEngine.MGL.Rendering;
using KirasaEngine.MGL.Rendering.RenderGraph;

namespace KirasaEngine.MGL.Rendering.RenderGraph.Passes;

/// <summary>
/// Applies bloom to the HDR scene color.
/// </summary>
public class BloomPass : RenderPass
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BloomPass"/> class.
    /// </summary>
    public BloomPass() : base("Bloom", new[] { RenderGraphTextureUsage.HDR }, new[] { RenderGraphTextureUsage.Bloom })
    {
    }
    
    /// <inheritdoc/>
    public override void Execute(ICommandList cmd, RenderContext context)
    {
        var hdrTexture = context.RenderGraph.GetTexture(RenderGraphTextureUsage.HDR);
        var bloomATarget = context.RenderGraph.GetRenderTarget(RenderGraphTextureUsage.Bloom);
        
        // Horizontal blur + bright pass
        BlurPass(cmd, context, hdrTexture, bloomATarget, true, true);
        // Vertical blur - for now just use bloomATarget as both source and target
        // This is a simplified version - in a full implementation we'd have separate passes
        BlurPass(cmd, context, bloomATarget.ColorTexture, bloomATarget, false, false);
    }
    
    private void BlurPass(ICommandList cmd, RenderContext context, ITexture source, IRenderTarget target, bool horizontal, bool applyThreshold)
    {
        var pipeline = context.ResourceManager.GetOrCreatePipeline(
            $"Blur_{(horizontal ? "Horizontal" : "Vertical")}",
            new PipelineDescription
            {
                ShaderSet = context.ShaderCompiler.CompileShaderSet("Blur", Array.Empty<VertexLayoutDescription>()),
                ResourceLayout = context.ResourceManager.GetOrCreateResourceLayout("Blur", ShaderResourceLayouts.Blur),
                CullMode = CullMode.None,
                ColorFormat = TextureFormat.Rgba16Float,
            });
        
        var constants = new ShaderResourceLayouts.BlurConstantsData
        {
            Params0 = new Vector4(
                1f / context.Width,
                1f / context.Height,
                horizontal ? 0f : 1f,
                context.Settings.BloomBlurRadius),
            Params1 = new Vector4(
                context.Settings.BloomThreshold,
                applyThreshold ? 1f : 0f,
                0, 0),
        };
        
        var constantsBuffer = context.ResourceManager.GetOrCreateBuffer(
            $"BlurConstants_{(horizontal ? "Horizontal" : "Vertical")}",
            new BufferDescription(ShaderResourceLayouts.BlurConstantsData.SizeInBytes, BufferUsage.Uniform | BufferUsage.Dynamic));
        
        context.ResourceManager.UploadBufferData(cmd, constantsBuffer, MemoryMarshal.AsBytes(new ReadOnlySpan<ShaderResourceLayouts.BlurConstantsData>(ref constants)));
        
        var resourceSet = context.ResourceManager.AllocateDescriptorSet(
            pipeline.Description.ResourceLayout,
            new object[] { constantsBuffer, source, context.ResourceManager.GetOrCreateSampler("Clamp", new SamplerDescription(SamplerFilter.Linear, SamplerAddressMode.Clamp)) });
        
        cmd.Begin();
        cmd.SetRenderTarget(target);
        cmd.SetViewport(new Viewport(0, 0, context.Width, context.Height));
        cmd.SetPipeline(pipeline);
        cmd.SetResourceSet(0, resourceSet);
        cmd.Draw(3); // Fullscreen triangle
        cmd.End();
        context.Device.Submit(cmd);
    }
}





