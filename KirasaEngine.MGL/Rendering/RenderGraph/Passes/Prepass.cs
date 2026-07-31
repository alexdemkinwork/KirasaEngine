using System;
using System.Numerics;
using System.Runtime.InteropServices;

using KirasaEngine.MGL.Instancing;
using KirasaEngine.MGL.Models;
using KirasaEngine.MGL.Rendering;
using KirasaEngine.MGL.Rendering.RenderGraph;

namespace KirasaEngine.MGL.Rendering.RenderGraph.Passes;

/// <summary>
/// Renders depth and normals from the camera's perspective for SSAO.
/// </summary>
public class Prepass : RenderPass
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Prepass"/> class.
    /// </summary>
    public Prepass() : base("Prepass", Array.Empty<RenderGraphTextureUsage>(), new[] { RenderGraphTextureUsage.Normal })
    {
    }
    
    /// <inheritdoc/>
    public override void Execute(ICommandList cmd, RenderContext context)
    {
        // Prepass outputs both depth and normal in a single RGBA16F render target (normal.xyz, depth.w)
        // Use the Normal texture from RenderGraph as the primary output
        var renderTarget = context.RenderGraph.GetRenderTarget(RenderGraphTextureUsage.Normal);
        
        var vertexLayouts = new VertexLayoutDescription[]
        {
            VertexPNCT.GetVertexLayout(),
            InstanceData.GetVertexLayout(4) // Instance data starts at location 4
        };
        
        var pipeline = context.ResourceManager.GetOrCreatePipeline(
            "Prepass",
            new PipelineDescription
            {
                ShaderSet = context.ShaderCompiler.CompileShaderSet("DepthNormalPrepass", vertexLayouts),
                ResourceLayout = context.ResourceManager.GetOrCreateResourceLayout("Prepass", ShaderResourceLayouts.Prepass),
                CullMode = CullMode.Back,
                ColorFormat = TextureFormat.Rgba16Float,
                DepthFormat = TextureFormat.Depth24Stencil8,
            });
        
        var constants = new ShaderResourceLayouts.PrepassConstantsData
        {
            ViewProjection = context.GetViewProjectionMatrix(),
            View = context.Camera.GetViewMatrix(context.CameraTransform),
        };
        
        var constantsBuffer = context.ResourceManager.GetOrCreateBuffer(
            "PrepassConstants",
            new BufferDescription(ShaderResourceLayouts.PrepassConstantsData.SizeInBytes, BufferUsage.Uniform | BufferUsage.Dynamic));
        
        context.ResourceManager.UploadBufferData(cmd, constantsBuffer, MemoryMarshal.AsBytes(new ReadOnlySpan<ShaderResourceLayouts.PrepassConstantsData>(ref constants)));
        
        var resourceSet = context.ResourceManager.AllocateDescriptorSet(
            pipeline.Description.ResourceLayout,
            new[] { constantsBuffer });
        
        cmd.SetRenderTarget(renderTarget);
        cmd.SetViewport(new Viewport(0, 0, context.Width, context.Height));
        cmd.ClearColor(new Vector4(0, 0, 0, 1000f));
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






