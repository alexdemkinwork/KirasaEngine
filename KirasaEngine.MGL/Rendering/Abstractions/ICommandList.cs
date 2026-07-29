namespace KirasaEngine.MGL.Rendering.Abstractions;

public interface ICommandList : IDisposable
{
    void Begin();
    void End();

    /// <summary>Null targets the swap chain backbuffer.</summary>
    void SetRenderTarget(IRenderTarget? target);
    void ClearColor(Vector4 color);
    void ClearDepthStencil(float depth = 1f, byte stencil = 0);
    void SetViewport(in Viewport viewport);
    void SetScissor(in RectI rect);
    void SetPipeline(IPipeline pipeline);
    void SetVertexBuffer(uint slot, IBuffer buffer, uint offset = 0);
    void SetIndexBuffer(IBuffer buffer, IndexFormat format, uint offset = 0);
    void SetResourceSet(uint slot, IResourceSet resourceSet);

    /// <summary>Uploads CPU data into a <see cref="BufferUsage.Dynamic"/> buffer (e.g. per-frame uniforms/instance data).</summary>
    void UpdateBuffer(IBuffer buffer, ReadOnlySpan<byte> data, uint destinationOffsetBytes = 0);

    void DrawIndexed(uint indexCount, uint instanceCount = 1, uint firstIndex = 0, int vertexOffset = 0, uint firstInstance = 0);
}
