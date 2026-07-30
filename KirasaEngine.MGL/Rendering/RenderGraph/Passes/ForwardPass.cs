using System;
using System.Numerics;
using System.Runtime.InteropServices;

using KirasaEngine.MGL.Rendering;
using KirasaEngine.MGL.Rendering.RenderGraph;

namespace KirasaEngine.MGL.Rendering.RenderGraph.Passes;

/// <summary>
/// Renders the scene with lighting, shadows, and AO.
/// </summary>
public class ForwardPass : RenderPass
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ForwardPass"/> class.
    /// </summary>
    public ForwardPass() : base("Forward", new[] { RenderGraphTextureUsage.ShadowMap, RenderGraphTextureUsage.AO }, new[] { RenderGraphTextureUsage.HDR })
    {
    }
    
    /// <inheritdoc/>
    public override void Execute(ICommandList cmd, RenderContext context)
    {
        var renderTarget = context.ResourceManager.CreateRenderTarget(
            "Forward_RT",
            new TextureDescription(context.Width, context.Height, TextureFormat.Rgba16Float, TextureUsage.RenderTarget));
        
        // Note: depth is handled by the render target internally
        
        cmd.Begin();
        cmd.SetRenderTarget(renderTarget);
        cmd.SetViewport(new Viewport(0, 0, context.Width, context.Height));
        cmd.ClearColor(context.Scene.BackgroundColor);
        cmd.ClearDepthStencil();
        
        // Draw all geometry
        foreach (var node in context.Scene.Traverse())
        {
            if (node.Renderer == null) continue;
            DrawNode(cmd, node, context);
        }
        
        foreach (var batch in context.Scene.InstancedBatches)
        {
            if (batch.Instances.Count == 0) continue;
            DrawBatch(cmd, batch, context);
        }
        
        cmd.End();
        context.Device.Submit(cmd);
    }
    
    private void DrawNode(ICommandList cmd, SceneNode node, RenderContext context)
    {
        var mesh = node.Renderer!.Mesh;
        var material = node.Renderer.Material;
        
        var pipeline = context.ResourceManager.GetOrCreatePipeline(
            $"Forward_{material.ShaderName}_{material.Blend}_{material.DoubleSided}",
            new PipelineDescription
            {
                ShaderSet = context.ShaderCompiler.CompileShaderSet(material.ShaderName, Array.Empty<VertexLayoutDescription>()),
                ResourceLayout = context.ResourceManager.GetOrCreateResourceLayout("Forward", ShaderResourceLayouts.Standard),
                CullMode = material.DoubleSided ? CullMode.None : CullMode.Back,
                Blend = material.Blend,
                ColorFormat = TextureFormat.Rgba16Float,
                DepthFormat = TextureFormat.Depth24Stencil8,
            });
        
        var frameConstants = new ShaderResourceLayouts.FrameConstantsData
        {
            ViewProjection = context.GetViewProjectionMatrix(),
            LightViewProjection = context.GetLightViewProjectionMatrix(),
            LightDirection = context.LightNode != null ? new Vector4(context.LightNode.Transform.Forward, 0) : new Vector4(0, -1, 0, 0),
            LightColor = context.LightNode != null ? new Vector4(context.LightNode.Light!.Color, context.LightNode.Light.Intensity) : new Vector4(1, 1, 1, 1),
            AmbientColor = new Vector4(context.Scene.AmbientColor, 1f),
            ShadowParams = new Vector4(
                1f / context.Settings.ShadowMapResolution,
                context.Settings.ShadowBias,
                context.Settings.ShadowPcfRadius,
                context.Settings.ShadowsActive ? 1f : 0f),
            ScreenParams = new Vector4(context.Width, context.Height, context.Settings.SSAOActive ? 1f : 0f, 0f),
            CameraPosition = new Vector4(context.CameraTransform.WorldPosition, 1f),
        };
        
        var drawConstants = new ShaderResourceLayouts.DrawConstantsData
        {
            BaseColor = material.BaseColor,
            SpecularParams = new Vector4(material.SpecularIntensity, material.Shininess, 0, 0),
        };
        
        var frameConstantsBuffer = context.ResourceManager.GetOrCreateBuffer(
            "FrameConstants",
            new BufferDescription(ShaderResourceLayouts.FrameConstantsData.SizeInBytes, BufferUsage.Uniform | BufferUsage.Dynamic));
        
        var drawConstantsBuffer = context.ResourceManager.GetOrCreateBuffer(
            $"DrawConstants_{material.GetHashCode()}",
            new BufferDescription(ShaderResourceLayouts.DrawConstantsData.SizeInBytes, BufferUsage.Uniform | BufferUsage.Dynamic));
        
        context.ResourceManager.UploadBufferData(cmd, frameConstantsBuffer, MemoryMarshal.AsBytes(new ReadOnlySpan<ShaderResourceLayouts.FrameConstantsData>(ref frameConstants)));
        context.ResourceManager.UploadBufferData(cmd, drawConstantsBuffer, MemoryMarshal.AsBytes(new ReadOnlySpan<ShaderResourceLayouts.DrawConstantsData>(ref drawConstants)));
        
        var shadowMap = context.Settings.ShadowsActive
            ? context.ResourceManager.GetOrCreateTexture("ShadowMap", new TextureDescription(context.Settings.ShadowMapResolution, context.Settings.ShadowMapResolution, TextureFormat.R32Float, TextureUsage.Sampled))
            : context.ResourceManager.GetOrCreateTexture("PlaceholderR32", new TextureDescription(1, 1, TextureFormat.R32Float, TextureUsage.Sampled), stackalloc byte[] { 0, 0, 0, 0 });
        
        var aoTexture = context.Settings.SSAOActive
            ? context.ResourceManager.GetOrCreateTexture("SSAO_AO", new TextureDescription(context.Width, context.Height, TextureFormat.R32Float, TextureUsage.Sampled))
            : context.ResourceManager.GetOrCreateTexture("PlaceholderR32", new TextureDescription(1, 1, TextureFormat.R32Float, TextureUsage.Sampled), stackalloc byte[] { 0, 0, 0, 0 });
        
        var resourceSet = context.ResourceManager.AllocateDescriptorSet(
            pipeline.Description.ResourceLayout,
            new object[]
            {
                frameConstantsBuffer,
                drawConstantsBuffer,
                material.BaseColorTexture ?? context.ResourceManager.GetOrCreateTexture("White", new TextureDescription(1, 1, TextureFormat.Rgba8UNorm, TextureUsage.Sampled), stackalloc byte[] { 255, 255, 255, 255 }),
                context.ResourceManager.GetOrCreateSampler("Linear", SamplerDescription.LinearWrap),
                shadowMap,
                context.ResourceManager.GetOrCreateSampler("Clamp", new SamplerDescription(SamplerFilter.Linear, SamplerAddressMode.Clamp)),
                aoTexture,
                context.ResourceManager.GetOrCreateSampler("Clamp", new SamplerDescription(SamplerFilter.Linear, SamplerAddressMode.Clamp)),
            });
        
        var meshRes = context.ResourceManager.GetOrCreateBuffer(
            $"Mesh_{mesh.GetHashCode()}_Vertices",
            new BufferDescription((uint)MemoryMarshal.AsBytes(mesh.Vertices.ToArray().AsSpan()).Length, BufferUsage.Vertex),
            MemoryMarshal.AsBytes(mesh.Vertices.ToArray().AsSpan()));
        
        var indexRes = context.ResourceManager.GetOrCreateBuffer(
            $"Mesh_{mesh.GetHashCode()}_Indices",
            new BufferDescription((uint)MemoryMarshal.AsBytes(mesh.Indices.ToArray().AsSpan()).Length, BufferUsage.Index),
            MemoryMarshal.AsBytes(mesh.Indices.ToArray().AsSpan()));
        
        var instanceData = new InstanceData(node.Transform.WorldMatrix, Vector4.One);
        var instanceBuffer = context.ResourceManager.GetOrCreateBuffer(
            $"Instance_{node.GetHashCode()}",
            new BufferDescription(InstanceData.SizeInBytes, BufferUsage.Vertex | BufferUsage.Dynamic),
            MemoryMarshal.AsBytes(new ReadOnlySpan<InstanceData>(ref instanceData)));
        
        cmd.SetPipeline(pipeline);
        cmd.SetVertexBuffer(0, meshRes);
        cmd.SetVertexBuffer(1, instanceBuffer);
        cmd.SetIndexBuffer(indexRes, IndexFormat.UInt32);
        cmd.SetResourceSet(0, resourceSet);
        cmd.DrawIndexed((uint)mesh.Indices.Length, 1);
    }
    
    private void DrawBatch(ICommandList cmd, InstancedBatch batch, RenderContext context)
    {
        var mesh = batch.Mesh;
        var material = batch.Material;
        
        var pipeline = context.ResourceManager.GetOrCreatePipeline(
            $"Forward_{material.ShaderName}_{material.Blend}_{material.DoubleSided}",
            new PipelineDescription
            {
                ShaderSet = context.ShaderCompiler.CompileShaderSet(material.ShaderName, Array.Empty<VertexLayoutDescription>()),
                ResourceLayout = context.ResourceManager.GetOrCreateResourceLayout("Forward", ShaderResourceLayouts.Standard),
                CullMode = material.DoubleSided ? CullMode.None : CullMode.Back,
                Blend = material.Blend,
                ColorFormat = TextureFormat.Rgba16Float,
                DepthFormat = TextureFormat.Depth24Stencil8,
            });
        
        var frameConstants = new ShaderResourceLayouts.FrameConstantsData
        {
            ViewProjection = context.GetViewProjectionMatrix(),
            LightViewProjection = context.GetLightViewProjectionMatrix(),
            LightDirection = context.LightNode != null ? new Vector4(context.LightNode.Transform.Forward, 0) : new Vector4(0, -1, 0, 0),
            LightColor = context.LightNode != null ? new Vector4(context.LightNode.Light!.Color, context.LightNode.Light.Intensity) : new Vector4(1, 1, 1, 1),
            AmbientColor = new Vector4(context.Scene.AmbientColor, 1f),
            ShadowParams = new Vector4(
                1f / context.Settings.ShadowMapResolution,
                context.Settings.ShadowBias,
                context.Settings.ShadowPcfRadius,
                context.Settings.ShadowsActive ? 1f : 0f),
            ScreenParams = new Vector4(context.Width, context.Height, context.Settings.SSAOActive ? 1f : 0f, 0f),
            CameraPosition = new Vector4(context.CameraTransform.WorldPosition, 1f),
        };
        
        var drawConstants = new ShaderResourceLayouts.DrawConstantsData
        {
            BaseColor = material.BaseColor,
            SpecularParams = new Vector4(material.SpecularIntensity, material.Shininess, 0, 0),
        };
        
        var frameConstantsBuffer = context.ResourceManager.GetOrCreateBuffer(
            "FrameConstants",
            new BufferDescription(ShaderResourceLayouts.FrameConstantsData.SizeInBytes, BufferUsage.Uniform | BufferUsage.Dynamic));
        
        var drawConstantsBuffer = context.ResourceManager.GetOrCreateBuffer(
            $"DrawConstants_{material.GetHashCode()}",
            new BufferDescription(ShaderResourceLayouts.DrawConstantsData.SizeInBytes, BufferUsage.Uniform | BufferUsage.Dynamic));
        
        context.ResourceManager.UploadBufferData(cmd, frameConstantsBuffer, MemoryMarshal.AsBytes(new ReadOnlySpan<ShaderResourceLayouts.FrameConstantsData>(ref frameConstants)));
        context.ResourceManager.UploadBufferData(cmd, drawConstantsBuffer, MemoryMarshal.AsBytes(new ReadOnlySpan<ShaderResourceLayouts.DrawConstantsData>(ref drawConstants)));
        
        var shadowMap = context.Settings.ShadowsActive
            ? context.ResourceManager.GetOrCreateTexture("ShadowMap", new TextureDescription(context.Settings.ShadowMapResolution, context.Settings.ShadowMapResolution, TextureFormat.R32Float, TextureUsage.Sampled))
            : context.ResourceManager.GetOrCreateTexture("PlaceholderR32", new TextureDescription(1, 1, TextureFormat.R32Float, TextureUsage.Sampled), stackalloc byte[] { 0, 0, 0, 0 });
        
        var aoTexture = context.Settings.SSAOActive
            ? context.ResourceManager.GetOrCreateTexture("SSAO_AO", new TextureDescription(context.Width, context.Height, TextureFormat.R32Float, TextureUsage.Sampled))
            : context.ResourceManager.GetOrCreateTexture("PlaceholderR32", new TextureDescription(1, 1, TextureFormat.R32Float, TextureUsage.Sampled), stackalloc byte[] { 0, 0, 0, 0 });
        
        var resourceSet = context.ResourceManager.AllocateDescriptorSet(
            pipeline.Description.ResourceLayout,
            new object[]
            {
                frameConstantsBuffer,
                drawConstantsBuffer,
                material.BaseColorTexture ?? context.ResourceManager.GetOrCreateTexture("White", new TextureDescription(1, 1, TextureFormat.Rgba8UNorm, TextureUsage.Sampled), stackalloc byte[] { 255, 255, 255, 255 }),
                context.ResourceManager.GetOrCreateSampler("Linear", SamplerDescription.LinearWrap),
                shadowMap,
                context.ResourceManager.GetOrCreateSampler("Clamp", new SamplerDescription(SamplerFilter.Linear, SamplerAddressMode.Clamp)),
                aoTexture,
                context.ResourceManager.GetOrCreateSampler("Clamp", new SamplerDescription(SamplerFilter.Linear, SamplerAddressMode.Clamp)),
            });
        
        var meshRes = context.ResourceManager.GetOrCreateBuffer(
            $"Mesh_{mesh.GetHashCode()}_Vertices",
            new BufferDescription((uint)MemoryMarshal.AsBytes(mesh.Vertices.ToArray().AsSpan()).Length, BufferUsage.Vertex),
            MemoryMarshal.AsBytes(mesh.Vertices.ToArray().AsSpan()));
        
        var indexRes = context.ResourceManager.GetOrCreateBuffer(
            $"Mesh_{mesh.GetHashCode()}_Indices",
            new BufferDescription((uint)MemoryMarshal.AsBytes(mesh.Indices.ToArray().AsSpan()).Length, BufferUsage.Index),
            MemoryMarshal.AsBytes(mesh.Indices.ToArray().AsSpan()));
        
        var instanceBuffer = context.ResourceManager.GetOrCreateBuffer(
            $"Batch_{batch.GetHashCode()}",
            new BufferDescription((uint)batch.Instances.Count * InstanceData.SizeInBytes, BufferUsage.Vertex | BufferUsage.Dynamic),
            MemoryMarshal.AsBytes(batch.Instances.ToArray().AsSpan()));
        
        cmd.SetPipeline(pipeline);
        cmd.SetVertexBuffer(0, meshRes);
        cmd.SetVertexBuffer(1, instanceBuffer);
        cmd.SetIndexBuffer(indexRes, IndexFormat.UInt32);
        cmd.SetResourceSet(0, resourceSet);
        cmd.DrawIndexed((uint)mesh.Indices.Length, (uint)batch.Instances.Count);
    }
}






