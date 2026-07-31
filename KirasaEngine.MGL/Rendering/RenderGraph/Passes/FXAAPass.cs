using System;
using System.Numerics;
using System.Runtime.InteropServices;

using KirasaEngine.MGL.Rendering;
using KirasaEngine.MGL.Rendering.RenderGraph;

namespace KirasaEngine.MGL.Rendering.RenderGraph.Passes;

/// <summary>
/// Applies FXAA to the LDR scene color.
/// </summary>
public class FXAAPass : RenderPass
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FXAAPass"/> class.
    /// </summary>
    public FXAAPass() : base("FXAA", new[] { RenderGraphTextureUsage.LDR }, new[] { RenderGraphTextureUsage.Final })
    {
    }
    
    /// <inheritdoc/>
    public override void Execute(ICommandList cmd, RenderContext context)
    {
        var ldrTexture = context.RenderGraph.GetTexture(RenderGraphTextureUsage.LDR);
        
        var finalTarget = context.RenderGraph.GetRenderTarget(RenderGraphTextureUsage.Final);
        
        var pipeline = context.ResourceManager.GetOrCreatePipeline(
            "FXAA",
            new PipelineDescription
            {
                ShaderSet = context.ShaderCompiler.CompileShaderSet("FXAA", Array.Empty<VertexLayoutDescription>()),
                ResourceLayout = context.ResourceManager.GetOrCreateResourceLayout("FXAA", ShaderResourceLayouts.FXAA),
                CullMode = CullMode.None,
                ColorFormat = TextureFormat.Rgba8UNorm,
            });
        
        var constants = new ShaderResourceLayouts.FXAAConstantsData
        {
            Params0 = new Vector4(1f / context.Width, 1f / context.Height, 0, 0),
        };
        
        var constantsBuffer = context.ResourceManager.GetOrCreateBuffer(
            "FXAAConstants",
            new BufferDescription(ShaderResourceLayouts.FXAAConstantsData.SizeInBytes, BufferUsage.Uniform | BufferUsage.Dynamic));
        
        context.ResourceManager.UploadBufferData(cmd, constantsBuffer, MemoryMarshal.AsBytes(new ReadOnlySpan<ShaderResourceLayouts.FXAAConstantsData>(ref constants)));
        
        var resourceSet = context.ResourceManager.AllocateDescriptorSet(
            pipeline.Description.ResourceLayout,
            new object[] { constantsBuffer, ldrTexture, context.ResourceManager.GetOrCreateSampler("Clamp", new SamplerDescription(SamplerFilter.Linear, SamplerAddressMode.Clamp)) });
        
        cmd.SetRenderTarget(finalTarget);
        cmd.SetViewport(new Viewport(0, 0, context.Width, context.Height));
        cmd.SetPipeline(pipeline);
        cmd.SetResourceSet(0, resourceSet);
        cmd.Draw(3); // Fullscreen triangle
    }
}






