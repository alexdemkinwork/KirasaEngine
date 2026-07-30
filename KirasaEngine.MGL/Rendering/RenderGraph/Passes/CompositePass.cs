using System;
using System.Numerics;
using System.Runtime.InteropServices;

using KirasaEngine.MGL.Rendering;
using KirasaEngine.MGL.Rendering.RenderGraph;

namespace KirasaEngine.MGL.Rendering.RenderGraph.Passes;

/// <summary>
/// Combines HDR and bloom, applies tonemapping, and outputs LDR.
/// </summary>
public class CompositePass : RenderPass
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CompositePass"/> class.
    /// </summary>
    public CompositePass() : base("Composite", new[] { RenderGraphTextureUsage.HDR, RenderGraphTextureUsage.Bloom }, new[] { RenderGraphTextureUsage.LDR })
    {
    }
    
    /// <inheritdoc/>
    public override void Execute(ICommandList cmd, RenderContext context)
    {
        var hdrTexture = context.ResourceManager.GetOrCreateTexture(
            "Forward_HDR",
            new TextureDescription(context.Width, context.Height, TextureFormat.Rgba16Float, TextureUsage.Sampled));
        
        var bloomTexture = context.Settings.BloomActive
            ? context.ResourceManager.GetOrCreateTexture(
                "Bloom_B",
                new TextureDescription(context.Width, context.Height, TextureFormat.Rgba16Float, TextureUsage.Sampled))
            : null;
        
        var ldrTarget = context.ResourceManager.CreateRenderTarget(
            "Composite_LDR",
            new TextureDescription(context.Width, context.Height, TextureFormat.Rgba8UNorm, TextureUsage.RenderTarget));
        
        var pipeline = context.ResourceManager.GetOrCreatePipeline(
            "Composite",
            new PipelineDescription
            {
                ShaderSet = context.ShaderCompiler.CompileShaderSet("Composite", Array.Empty<VertexLayoutDescription>()),
                ResourceLayout = context.ResourceManager.GetOrCreateResourceLayout("Composite", ShaderResourceLayouts.Composite),
                CullMode = CullMode.None,
                ColorFormat = TextureFormat.Rgba8UNorm,
            });
        
        var constants = new ShaderResourceLayouts.CompositeConstantsData
        {
            Params0 = new Vector4(
                context.Settings.BloomIntensity,
                context.Settings.VignetteIntensity,
                context.Settings.Saturation,
                context.Settings.Contrast),
            Params1 = new Vector4(
                bloomTexture != null ? 1f : 0f,
                context.Settings.VignetteActive ? 1f : 0f,
                0, 0),
        };
        
        var constantsBuffer = context.ResourceManager.GetOrCreateBuffer(
            "CompositeConstants",
            new BufferDescription(ShaderResourceLayouts.CompositeConstantsData.SizeInBytes, BufferUsage.Uniform | BufferUsage.Dynamic));
        
        context.ResourceManager.UploadBufferData(cmd, constantsBuffer, MemoryMarshal.AsBytes(new ReadOnlySpan<ShaderResourceLayouts.CompositeConstantsData>(ref constants)));
        
        var resourceSet = context.ResourceManager.AllocateDescriptorSet(
            pipeline.Description.ResourceLayout,
            new object[]
            {
                constantsBuffer,
                hdrTexture,
                context.ResourceManager.GetOrCreateSampler("Clamp", new SamplerDescription(SamplerFilter.Linear, SamplerAddressMode.Clamp)),
                bloomTexture ?? hdrTexture,
                context.ResourceManager.GetOrCreateSampler("Clamp", new SamplerDescription(SamplerFilter.Linear, SamplerAddressMode.Clamp)),
            });
        
        cmd.Begin();
        cmd.SetRenderTarget(ldrTarget);
        cmd.SetViewport(new Viewport(0, 0, context.Width, context.Height));
        cmd.SetPipeline(pipeline);
        cmd.SetResourceSet(0, resourceSet);
        cmd.Draw(3); // Fullscreen triangle
        cmd.End();
        context.Device.Submit(cmd);
    }
}






