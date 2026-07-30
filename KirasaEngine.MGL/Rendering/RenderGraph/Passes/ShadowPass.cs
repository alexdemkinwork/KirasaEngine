using System;
using System.Numerics;
using System.Runtime.InteropServices;

using KirasaEngine.MGL.Rendering;
using KirasaEngine.MGL.Rendering.RenderGraph;

namespace KirasaEngine.MGL.Rendering.RenderGraph.Passes;

/// <summary>
/// Renders depth from the light's perspective for shadow mapping.
/// </summary>
public class ShadowPass : RenderPass
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ShadowPass"/> class.
    /// </summary>
    public ShadowPass() : base("Shadow", Array.Empty<RenderGraphTextureUsage>(), new[] { RenderGraphTextureUsage.ShadowMap })
    {
    }
    
    /// <inheritdoc/>
    public override void Execute(ICommandList cmd, RenderContext context)
    {
        var target = context.ResourceManager.CreateRenderTarget(
            "ShadowMap",
            new TextureDescription(
                context.Settings.ShadowMapResolution,
                context.Settings.ShadowMapResolution,
                TextureFormat.R32Float,
                TextureUsage.RenderTarget));
        
        var pipeline = context.ResourceManager.GetOrCreatePipeline(
            "Shadow",
            new PipelineDescription
            {
                ShaderSet = context.ShaderCompiler.CompileShaderSet("ShadowDepth", Array.Empty<VertexLayoutDescription>()),
                ResourceLayout = context.ResourceManager.GetOrCreateResourceLayout("Shadow", ShaderResourceLayouts.Shadow),
                CullMode = CullMode.None,
                ColorFormat = TextureFormat.R32Float,
                DepthFormat = TextureFormat.Depth24Stencil8,
            });
        
        var constants = new ShaderResourceLayouts.ShadowConstantsData
        {
            LightViewProjection = context.GetLightViewProjectionMatrix(),
        };
        
        var constantsBuffer = context.ResourceManager.GetOrCreateBuffer(
            "ShadowConstants",
            new BufferDescription(ShaderResourceLayouts.ShadowConstantsData.SizeInBytes, BufferUsage.Uniform | BufferUsage.Dynamic));
        
        context.ResourceManager.UploadBufferData(cmd, constantsBuffer, MemoryMarshal.AsBytes(new ReadOnlySpan<ShaderResourceLayouts.ShadowConstantsData>(ref constants)));
        
        var resourceSet = context.ResourceManager.AllocateDescriptorSet(
            pipeline.Description.ResourceLayout,
            new[] { constantsBuffer });
        
        cmd.Begin();
        cmd.SetRenderTarget(target);
        cmd.SetViewport(new Viewport(0, 0, target.Width, target.Height));
        cmd.ClearColor(new Vector4(1, 1, 1, 1));
        cmd.ClearDepthStencil();
        cmd.SetPipeline(pipeline);
        cmd.SetResourceSet(0, resourceSet);
        
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
        
        cmd.SetVertexBuffer(0, meshRes);
        cmd.SetVertexBuffer(1, instanceBuffer);
        cmd.SetIndexBuffer(indexRes, IndexFormat.UInt32);
        cmd.DrawIndexed((uint)mesh.Indices.Length, 1);
    }
    
    private void DrawBatch(ICommandList cmd, InstancedBatch batch, RenderContext context)
    {
        var mesh = batch.Mesh;
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
        
        cmd.SetVertexBuffer(0, meshRes);
        cmd.SetVertexBuffer(1, instanceBuffer);
        cmd.SetIndexBuffer(indexRes, IndexFormat.UInt32);
        cmd.DrawIndexed((uint)mesh.Indices.Length, (uint)batch.Instances.Count);
    }
}






