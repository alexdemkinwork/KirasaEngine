using Silk.NET.Direct3D11;
using Silk.NET.Maths;

// Both the abstraction and Silk.NET define a "Viewport"; alias them apart for this file.
using Viewport = KirasaEngine.MGL.Rendering.Abstractions.Structs.Viewport;
using D3D11Viewport = Silk.NET.Direct3D11.Viewport;

namespace KirasaEngine.MGL.Rendering.Backends.Direct3D11;

/// <summary>
/// Records straight into the D3D11 immediate context. Just like the OpenGL backend there is no real
/// deferred command buffer here, so Begin/End are no-ops and <see cref="D3D11GraphicsDevice.Submit"/> only
/// has to flush. A deferred context would buy nothing given the synchronous-submit design.
/// </summary>
internal sealed unsafe class D3D11CommandList(ID3D11DeviceContext* context) : ICommandList
{
    private D3D11Pipeline? _currentPipeline;

    // D3D11's clear calls take the view explicitly rather than acting on "whatever is bound", so the
    // currently bound views have to be remembered between SetRenderTarget and ClearColor/ClearDepthStencil.
    private ID3D11RenderTargetView* _renderTargetView;
    private ID3D11DepthStencilView* _depthStencilView;

    // Scissoring is baked into the rasterizer state in D3D11, so the pipeline carries both variants and
    // this flag decides which one SetPipeline binds.
    private bool _scissorEnabled;

    public void Begin() { }
    public void End() { }

    public void SetRenderTarget(IRenderTarget? target)
    {
        if (target is D3D11RenderTarget d3dTarget)
        {
            _renderTargetView = d3dTarget.RenderTargetView;
            _depthStencilView = d3dTarget.DepthStencilView;

            var rtv = _renderTargetView;
            context->OMSetRenderTargets(1, &rtv, _depthStencilView);
        }
        else
        {
            // There is no swap-chain backbuffer in this backend, so "null target" can only mean "unbind".
            _renderTargetView = null;
            _depthStencilView = null;
            context->OMSetRenderTargets(0, (ID3D11RenderTargetView**)null, (ID3D11DepthStencilView*)null);
        }
    }

    public void ClearColor(Vector4 color)
    {
        if (_renderTargetView is null) return;

        var rgba = stackalloc float[4];
        rgba[0] = color.X;
        rgba[1] = color.Y;
        rgba[2] = color.Z;
        rgba[3] = color.W;

        context->ClearRenderTargetView(_renderTargetView, rgba);
    }

    public void ClearDepthStencil(float depth = 1f, byte stencil = 0)
    {
        if (_depthStencilView is null) return;

        context->ClearDepthStencilView(
            _depthStencilView,
            (uint)(ClearFlag.Depth | ClearFlag.Stencil),
            depth,
            stencil);
    }

    public void SetViewport(in Viewport viewport)
    {
        // Camera already produces D3D-native [0,1] depth, so MinDepth/MaxDepth pass straight through (the
        // OpenGL backend's ClipControl fix-up has no counterpart here).
        var vp = new D3D11Viewport(
            viewport.X, viewport.Y, viewport.Width, viewport.Height, viewport.MinDepth, viewport.MaxDepth);

        context->RSSetViewports(1, &vp);
    }

    public void SetScissor(in RectI rect)
    {
        // D3D11 RECTs are exclusive on the right/bottom edge.
        var box = new Box2D<int>(rect.X, rect.Y, rect.X + rect.Width, rect.Y + rect.Height);
        context->RSSetScissorRects(1, &box);

        _scissorEnabled = true;
        if (_currentPipeline is not null)
            context->RSSetState(_currentPipeline.RasterizerStateScissor);
    }

    public void SetPipeline(IPipeline pipeline)
    {
        _currentPipeline = (D3D11Pipeline)pipeline;
        var shaderSet = _currentPipeline.ShaderSet;

        context->VSSetShader(shaderSet.VertexShader, (ID3D11ClassInstance**)null, 0);
        context->PSSetShader(shaderSet.PixelShader, (ID3D11ClassInstance**)null, 0);

        context->IASetInputLayout(_currentPipeline.InputLayout);
        context->IASetPrimitiveTopology(_currentPipeline.Topology);

        context->RSSetState(_scissorEnabled ? _currentPipeline.RasterizerStateScissor : _currentPipeline.RasterizerState);
        context->OMSetDepthStencilState(_currentPipeline.DepthStencilState, 0);

        var blendFactor = stackalloc float[4] { 1f, 1f, 1f, 1f };
        context->OMSetBlendState(_currentPipeline.BlendState, blendFactor, 0xFFFFFFFF);
    }

    public void SetVertexBuffer(uint slot, IBuffer buffer, uint offset = 0)
    {
        if (_currentPipeline is null) throw new InvalidOperationException("SetPipeline must be called before SetVertexBuffer.");
        if (slot >= _currentPipeline.ShaderSet.VertexLayouts.Length)
            throw new ArgumentOutOfRangeException(nameof(slot), "No vertex layout registered for this slot.");

        // The stride is a property of the layout, not of the buffer, so it comes from the bound pipeline —
        // same lookup the OpenGL backend does to build its attribute pointers.
        var stride = _currentPipeline.ShaderSet.VertexLayouts[slot].Stride;
        var handle = ((D3D11Buffer)buffer).Handle;
        var byteOffset = offset;

        context->IASetVertexBuffers(slot, 1, &handle, &stride, &byteOffset);
    }

    public void SetIndexBuffer(IBuffer buffer, IndexFormat format, uint offset = 0) =>
        context->IASetIndexBuffer(((D3D11Buffer)buffer).Handle, D3D11Formats.MapIndexFormat(format), offset);

    public void SetResourceSet(uint slot, IResourceSet resourceSet)
    {
        // D3D11 has no descriptor sets: the "set" is unpacked here into individual per-stage binds. The
        // element's Binding is used verbatim as the b/t/s register number — those are independent
        // namespaces in HLSL, exactly as ShaderResourceLayouts documents.
        var d3dSet = (D3D11ResourceSet)resourceSet;
        var elements = d3dSet.Layout.Description.Elements;

        for (var i = 0; i < elements.Length; i++)
        {
            var element = elements[i];
            var resource = d3dSet.Resources[i];

            switch (element.Kind)
            {
                case ResourceKind.UniformBuffer:
                {
                    var handle = ((D3D11Buffer)resource).Handle;
                    if (element.Stages.HasFlag(ShaderStage.Vertex))
                        context->VSSetConstantBuffers(element.Binding, 1, &handle);
                    if (element.Stages.HasFlag(ShaderStage.Fragment))
                        context->PSSetConstantBuffers(element.Binding, 1, &handle);
                    break;
                }
                case ResourceKind.TextureReadOnly:
                {
                    var srv = ((D3D11Texture)resource).ShaderResourceView;
                    if (element.Stages.HasFlag(ShaderStage.Vertex))
                        context->VSSetShaderResources(element.Binding, 1, &srv);
                    if (element.Stages.HasFlag(ShaderStage.Fragment))
                        context->PSSetShaderResources(element.Binding, 1, &srv);
                    break;
                }
                case ResourceKind.Sampler:
                {
                    var sampler = ((D3D11Sampler)resource).Handle;
                    if (element.Stages.HasFlag(ShaderStage.Vertex))
                        context->VSSetSamplers(element.Binding, 1, &sampler);
                    if (element.Stages.HasFlag(ShaderStage.Fragment))
                        context->PSSetSamplers(element.Binding, 1, &sampler);
                    break;
                }
            }
        }

        _ = slot;
    }

    public void UpdateBuffer(IBuffer buffer, ReadOnlySpan<byte> data, uint destinationOffsetBytes = 0) =>
        ((D3D11Buffer)buffer).SetData(data, destinationOffsetBytes);

    public void DrawIndexed(uint indexCount, uint instanceCount = 1, uint firstIndex = 0, int vertexOffset = 0, uint firstInstance = 0)
    {
        if (_currentPipeline is null) throw new InvalidOperationException("SetPipeline must be called before DrawIndexed.");

        context->DrawIndexedInstanced(indexCount, instanceCount, firstIndex, vertexOffset, firstInstance);
    }

    /// <summary>Nothing to release: the immediate context is owned by the device (mirrors GLCommandList).</summary>
    public void Dispose() { }
}
