using Silk.NET.OpenGL;

namespace KirasaEngine.MGL.Rendering.Backends.OpenGL;

/// <summary>
/// OpenGL has no real deferred command buffer, so every call executes immediately against the single
/// global GL context — Begin/End are no-ops and <see cref="IGraphicsDevice.Submit"/> requires nothing extra.
/// </summary>
internal sealed unsafe class GLCommandList(GL gl) : ICommandList
{
    private GLPipeline? _currentPipeline;
    private GLBuffer? _indexBuffer;
    private IndexFormat _indexFormat;
    private uint _indexBufferByteOffset;

    public void Begin() { }
    public void End() { }

    public void SetRenderTarget(IRenderTarget? target) =>
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, target is GLRenderTarget glTarget ? glTarget.FramebufferHandle : 0);

    public void ClearColor(Vector4 color)
    {
        gl.ClearColor(color.X, color.Y, color.Z, color.W);
        gl.Clear(ClearBufferMask.ColorBufferBit);
    }

    public void ClearDepthStencil(float depth = 1f, byte stencil = 0)
    {
        gl.DepthMask(true);
        gl.ClearDepth(depth);
        gl.ClearStencil(stencil);
        gl.Clear(ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);
    }

    public void SetViewport(in Viewport viewport)
    {
        gl.Viewport((int)viewport.X, (int)viewport.Y, (uint)viewport.Width, (uint)viewport.Height);
        gl.DepthRange(viewport.MinDepth, viewport.MaxDepth);
    }

    public void SetScissor(in RectI rect)
    {
        gl.Enable(EnableCap.ScissorTest);
        gl.Scissor(rect.X, rect.Y, (uint)rect.Width, (uint)rect.Height);
    }

    public void SetPipeline(IPipeline pipeline)
    {
        _currentPipeline = (GLPipeline)pipeline;
        var desc = _currentPipeline.Description;

        gl.UseProgram(_currentPipeline.ShaderSet.ProgramHandle);
        gl.BindVertexArray(_currentPipeline.VertexArrayHandle);

        if (desc.DepthTestEnabled)
        {
            gl.Enable(EnableCap.DepthTest);
            gl.DepthFunc(GLFormats.MapCompare(desc.DepthCompare));
        }
        else
        {
            gl.Disable(EnableCap.DepthTest);
        }
        gl.DepthMask(desc.DepthWriteEnabled);

        if (desc.CullMode == CullMode.None)
        {
            gl.Disable(EnableCap.CullFace);
        }
        else
        {
            gl.Enable(EnableCap.CullFace);
            gl.CullFace(desc.CullMode == CullMode.Front ? TriangleFace.Front : TriangleFace.Back);
        }
        gl.FrontFace(desc.FrontFace == FrontFace.CounterClockwise ? Silk.NET.OpenGL.FrontFaceDirection.Ccw : Silk.NET.OpenGL.FrontFaceDirection.CW);

        gl.PolygonMode(GLEnum.FrontAndBack, desc.FillMode == FillMode.Solid ? PolygonMode.Fill : PolygonMode.Line);

        switch (desc.Blend)
        {
            case BlendMode.Opaque:
                gl.Disable(EnableCap.Blend);
                break;
            case BlendMode.AlphaBlend:
                gl.Enable(EnableCap.Blend);
                gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
                break;
            case BlendMode.Additive:
                gl.Enable(EnableCap.Blend);
                gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One);
                break;
        }
    }

    public void SetVertexBuffer(uint slot, IBuffer buffer, uint offset = 0)
    {
        if (_currentPipeline is null) throw new InvalidOperationException("SetPipeline must be called before SetVertexBuffer.");
        if (slot >= _currentPipeline.ShaderSet.VertexLayouts.Length)
            throw new ArgumentOutOfRangeException(nameof(slot), "No vertex layout registered for this slot.");

        var layout = _currentPipeline.ShaderSet.VertexLayouts[slot];
        var glBuffer = (GLBuffer)buffer;

        gl.BindBuffer(BufferTargetARB.ArrayBuffer, glBuffer.Handle);
        foreach (var element in layout.Elements)
        {
            var (count, type) = GLFormats.MapVertexElement(element.Format);
            gl.EnableVertexAttribArray(element.Location);
            gl.VertexAttribPointer(element.Location, count, type, false, layout.Stride, (void*)(nint)(offset + element.Offset));
            gl.VertexAttribDivisor(element.Location, layout.InputRate == VertexInputRate.PerInstance ? 1u : 0u);
        }
    }

    public void SetIndexBuffer(IBuffer buffer, IndexFormat format, uint offset = 0)
    {
        _indexBuffer = (GLBuffer)buffer;
        _indexFormat = format;
        _indexBufferByteOffset = offset;
        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _indexBuffer.Handle);
    }

    public void SetResourceSet(uint slot, IResourceSet resourceSet)
    {
        var glSet = (GLResourceSet)resourceSet;
        var elements = glSet.Layout.Description.Elements;

        for (var i = 0; i < elements.Length; i++)
        {
            var element = elements[i];
            var resource = glSet.Resources[i];

            switch (element.Kind)
            {
                case ResourceKind.UniformBuffer:
                    gl.BindBufferBase(BufferTargetARB.UniformBuffer, element.Binding, ((GLBuffer)resource).Handle);
                    break;
                case ResourceKind.TextureReadOnly:
                    gl.ActiveTexture(TextureUnit.Texture0 + (int)element.Binding);
                    gl.BindTexture(TextureTarget.Texture2D, ((GLTexture)resource).Handle);
                    break;
                case ResourceKind.Sampler:
                    gl.BindSampler(element.Binding, ((GLSampler)resource).Handle);
                    break;
            }
        }
    }

    public void UpdateBuffer(IBuffer buffer, ReadOnlySpan<byte> data, uint destinationOffsetBytes = 0) =>
        ((GLBuffer)buffer).SetData(data, destinationOffsetBytes);

    public void DrawIndexed(uint indexCount, uint instanceCount = 1, uint firstIndex = 0, int vertexOffset = 0, uint firstInstance = 0)
    {
        if (_currentPipeline is null) throw new InvalidOperationException("SetPipeline must be called before DrawIndexed.");

        var indexSize = _indexFormat == IndexFormat.UInt32 ? 4u : 2u;
        var byteOffset = _indexBufferByteOffset + firstIndex * indexSize;
        var topology = GLFormats.MapTopology(_currentPipeline.Description.Topology);
        var indexType = GLFormats.MapIndexFormat(_indexFormat);

        gl.DrawElementsInstancedBaseVertexBaseInstance(
            topology, indexCount, indexType, (void*)(nint)byteOffset, instanceCount, vertexOffset, firstInstance);
    }

    public void Dispose() { }
}
