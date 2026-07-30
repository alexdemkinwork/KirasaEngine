using System;
using System.Numerics;
using System.Runtime.InteropServices;

using KirasaEngine.MGL.Rendering;

namespace KirasaEngine.MGL.Rendering.RenderGraph.Passes;

/// <summary>
/// Computes screen-space ambient occlusion using depth and normals.
/// </summary>
public class SSAOPass : RenderPass
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SSAOPass"/> class.
    /// </summary>
    public SSAOPass() : base("SSAO", new[] { TextureUsage.Depth, TextureUsage.Normal }, new[] { TextureUsage.AO })
    {
    }
    
    /// <inheritdoc/>
    public override void Execute(IGraphicsCommandList cmd, RenderContext context)
    {
        var aoTarget = context.ResourceManager.GetOrCreateTexture(
            "SSAO_AO",
            new TextureDescription(context.Width, context.Height, TextureFormat.R32Float, TextureUsage.RenderTarget));
        
        var pipeline = context.ResourceManager.GetOrCreatePipeline(
            "SSAO",
            new PipelineDescription
            {
                ShaderSet = context.ShaderCompiler.CompileShaderSet("SSAO", Array.Empty<VertexLayoutDescription>()),
                ResourceLayout = context.ResourceManager.GetOrCreateResourceLayout("SSAO", ShaderResourceLayouts.SSAO),
                CullMode = CullMode.None,
                ColorFormat = TextureFormat.R32Float,
            });
        
        var depthTexture = context.ResourceManager.GetOrCreateTexture(
            "Prepass_Depth",
            new TextureDescription(context.Width, context.Height, TextureFormat.Depth24Stencil8, TextureUsage.Sampled));
        
        var normalTexture = context.ResourceManager.GetOrCreateTexture(
            "Prepass_Normal",
            new TextureDescription(context.Width, context.Height, TextureFormat.Rgba16Float, TextureUsage.Sampled));
        
        var constants = new ShaderResourceLayouts.SSAOConstantsData
        {
            Params0 = new Vector4(
                MathF.Tan(context.Camera.FieldOfViewRadians * 0.5f),
                context.Width / (float)context.Height,
                context.Settings.SSAORadius,
                context.Settings.SSAOPower),
            Params1 = new Vector4(context.Settings.SSAOSampleCount, 0.02f, 0, 0),
        };
        
        var constantsBuffer = context.ResourceManager.GetOrCreateBuffer(
            "SSAOConstants",
            new BufferDescription(ShaderResourceLayouts.SSAOConstantsData.SizeInBytes, BufferUsage.Uniform | BufferUsage.Dynamic));
        
        context.ResourceManager.UploadBufferData(cmd, constantsBuffer, MemoryMarshal.AsBytes(new ReadOnlySpan<ShaderResourceLayouts.SSAOConstantsData>(ref constants)));
        
        var resourceSet = context.ResourceManager.AllocateDescriptorSet(
            pipeline.ResourceLayout,
            new object[] { constantsBuffer, normalTexture, context.ResourceManager.GetOrCreateSampler("Clamp", new SamplerDescription(SamplerFilter.Linear, SamplerAddressMode.Clamp)) });
        
        cmd.Begin();
        cmd.SetRenderTarget(aoTarget);
        cmd.SetViewport(new Viewport(0, 0, context.Width, context.Height));
        cmd.SetPipeline(pipeline);
        cmd.SetResourceSet(0, resourceSet);
        cmd.Draw(3); // Fullscreen triangle
        cmd.End();
        context.Device.Submit(cmd);
    }
}