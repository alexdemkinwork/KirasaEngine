using Silk.NET.Direct3D12;
using Silk.NET.Maths;
using Viewport = KirasaEngine.MGL.Rendering.Abstractions.Structs.Viewport;

using KirasaEngine.MGL.Rendering;

using KirasaEngine.MGL.Rendering;

namespace KirasaEngine.MGL.Rendering.Backends.Direct3D12;

/// <summary>
/// Records into the device's single reusable ID3D12GraphicsCommandList. Safe to reuse because
/// <see cref="D3D12GraphicsDevice.Submit"/> blocks until the GPU has finished the previous recording.
///
/// <para>The three things D3D12 demands that OpenGL/D3D11 handle implicitly, and that this class is
/// responsible for:</para>
/// <list type="number">
///   <item><description><b>Resource state.</b> Attachments must be explicitly transitioned into
///   RENDER_TARGET / DEPTH_WRITE before they can be bound (see <see cref="SetRenderTarget"/>). The matching
///   transition to COPY_SOURCE and back lives in <see cref="D3D12GraphicsDevice.ReadRenderTarget"/>, which
///   leaves the target in RENDER_TARGET so the next frame's transition is a no-op.</description></item>
///   <item><description><b>Scissor rectangles.</b> D3D12 has no "scissor disabled" state — the rect starts
///   empty and clips everything away. <see cref="SetViewport"/> therefore also sets a full-viewport scissor,
///   so a caller that never calls <see cref="SetScissor"/> still renders.</description></item>
///   <item><description><b>Descriptor heaps.</b> The shader-visible CBV/SRV/UAV and SAMPLER heaps must be
///   bound before any root descriptor table is set; that happens once in <see cref="Begin"/>, not per
///   draw.</description></item>
/// </list>
/// </summary>
internal sealed unsafe class D3D12CommandList(D3D12GraphicsDevice device) : ICommandList
{
    private readonly ID3D12GraphicsCommandList* _list = device.NativeCommandList;

    private D3D12Pipeline? _currentPipeline;
    private D3D12RenderTarget? _currentTarget;

    public void Begin()
    {
        D3D12Util.Check(device.NativeCommandAllocator->Reset(), "ID3D12CommandAllocator::Reset");
        D3D12Util.Check(_list->Reset(device.NativeCommandAllocator, (ID3D12PipelineState*)null), "ID3D12GraphicsCommandList::Reset");

        // Bind both shader-visible heaps up front. Doing this per draw would be legal but is a documented
        // pipeline flush on some drivers, and this renderer only ever uses these two heaps.
        var heaps = stackalloc ID3D12DescriptorHeap*[2];
        heaps[0] = device.SrvHeap.Heap;
        heaps[1] = device.SamplerHeap.Heap;
        _list->SetDescriptorHeaps(2, heaps);

        _currentPipeline = null;
        _currentTarget = null;
    }

    public void End() => D3D12Util.Check(_list->Close(), "ID3D12GraphicsCommandList::Close");

    public void SetRenderTarget(IRenderTarget? target)
    {
        if (target is null)
            throw new NotSupportedException("The Direct3D12 backend is headless (no swap chain); pass an explicit render target.");

        var d3dTarget = (D3D12RenderTarget)target;
        _currentTarget = d3dTarget;

        // Whatever state the attachments were left in (COMMON on first use, RENDER_TARGET after a previous
        // frame's ReadRenderTarget), get them ready to be drawn into.
        d3dTarget.Color.TransitionTo(_list, ResourceStates.RenderTarget);
        d3dTarget.Depth?.TransitionTo(_list, ResourceStates.DepthWrite);

        var rtv = d3dTarget.Color.RtvHandle;
        if (d3dTarget.Depth is { } depth)
        {
            var dsv = depth.DsvHandle;
            _list->OMSetRenderTargets(1, &rtv, false, &dsv);
        }
        else
        {
            _list->OMSetRenderTargets(1, &rtv, false, null);
        }
    }

    public void ClearColor(Vector4 color)
    {
        if (_currentTarget is null) throw new InvalidOperationException("SetRenderTarget must be called before ClearColor.");

        var rgba = stackalloc float[4] { color.X, color.Y, color.Z, color.W };
        _list->ClearRenderTargetView(_currentTarget.Color.RtvHandle, rgba, 0, null);
    }

    public void ClearDepthStencil(float depth = 1f, byte stencil = 0)
    {
        if (_currentTarget?.Depth is not { } depthTexture) return;
        _list->ClearDepthStencilView(depthTexture.DsvHandle, ClearFlags.Depth | ClearFlags.Stencil, depth, stencil, 0, null);
    }

    public void SetViewport(in Viewport viewport)
    {
        var d3dViewport = new Silk.NET.Direct3D12.Viewport
        {
            TopLeftX = viewport.X,
            TopLeftY = viewport.Y,
            Width = viewport.Width,
            Height = viewport.Height,
            MinDepth = viewport.MinDepth,
            MaxDepth = viewport.MaxDepth,
        };
        _list->RSSetViewports(1, &d3dViewport);

        // See the class doc: an unset scissor rect in D3D12 is an *empty* one, which silently discards every
        // pixel. Default it to the whole viewport so DrawIndexed works without an explicit SetScissor.
        SetScissor(new RectI((int)viewport.X, (int)viewport.Y, (int)viewport.Width, (int)viewport.Height));
    }

    public void SetScissor(in RectI rect)
    {
        var scissor = new Box2D<int>(rect.X, rect.Y, rect.X + rect.Width, rect.Y + rect.Height);
        _list->RSSetScissorRects(1, &scissor);
    }

    public void SetPipeline(IPipeline pipeline)
    {
        _currentPipeline = (D3D12Pipeline)pipeline;

        // Root signature first: setting it invalidates all previously bound root arguments, so it has to
        // precede the SetGraphicsRoot* calls that SetResourceSet makes.
        _list->SetGraphicsRootSignature(_currentPipeline.RootSignature);
        _list->SetPipelineState(_currentPipeline.PipelineState);
        _list->IASetPrimitiveTopology(_currentPipeline.Topology);
    }

    public void SetVertexBuffer(uint slot, IBuffer buffer, uint offset = 0)
    {
        if (_currentPipeline is null) throw new InvalidOperationException("SetPipeline must be called before SetVertexBuffer.");

        var layouts = _currentPipeline.ShaderSet.VertexLayouts;
        if (slot >= layouts.Length)
            throw new ArgumentOutOfRangeException(nameof(slot), "No vertex layout registered for this slot.");

        var d3dBuffer = (D3D12Buffer)buffer;
        var view = new VertexBufferView
        {
            BufferLocation = d3dBuffer.GpuAddress + offset,
            SizeInBytes = d3dBuffer.SizeInBytes - offset,
            StrideInBytes = layouts[slot].Stride,
        };
        _list->IASetVertexBuffers(slot, 1, &view);
    }

    public void SetIndexBuffer(IBuffer buffer, IndexFormat format, uint offset = 0)
    {
        var d3dBuffer = (D3D12Buffer)buffer;
        var view = new IndexBufferView
        {
            BufferLocation = d3dBuffer.GpuAddress + offset,
            SizeInBytes = d3dBuffer.SizeInBytes - offset,
            Format = D3D12Formats.MapIndexFormat(format),
        };
        _list->IASetIndexBuffer(&view);
    }

    public void SetResourceSet(uint slot, IResourceSet resourceSet)
    {
        _ = slot; // The root signature is a single flat parameter list; there are no separate set slots.

        var d3dSet = (D3D12ResourceSet)resourceSet;
        var layout = (D3D12ResourceLayout)d3dSet.Layout;
        var elements = layout.Description.Elements;

        for (var i = 0; i < elements.Length; i++)
        {
            var rootIndex = layout.RootParameterIndices[i];
            var resource = d3dSet.Resources[i];

            switch (elements[i].Kind)
            {
                case ResourceKind.UniformBuffer:
                    // Root CBV: the address is read fresh here rather than cached in the resource set.
                    _list->SetGraphicsRootConstantBufferView(rootIndex, ((D3D12Buffer)resource).GpuAddress);
                    break;

                case ResourceKind.TextureReadOnly:
                    _list->SetGraphicsRootDescriptorTable(rootIndex, ((D3D12Texture)resource).SrvGpuHandle);
                    break;

                case ResourceKind.Sampler:
                    _list->SetGraphicsRootDescriptorTable(rootIndex, ((D3D12Sampler)resource).GpuHandle);
                    break;
            }
        }
    }

    public void UpdateBuffer(IBuffer buffer, ReadOnlySpan<byte> data, uint destinationOffsetBytes = 0) =>
        ((D3D12Buffer)buffer).SetData(data, destinationOffsetBytes);

    public void DrawIndexed(uint indexCount, uint instanceCount = 1, uint firstIndex = 0, int vertexOffset = 0, uint firstInstance = 0)
    {
        if (_currentPipeline is null) throw new InvalidOperationException("SetPipeline must be called before DrawIndexed.");
        _list->DrawIndexedInstanced(indexCount, instanceCount, firstIndex, vertexOffset, firstInstance);
    }

    public void Draw(uint vertexCount, uint instanceCount = 1, uint firstVertex = 0, uint firstInstance = 0)
    {
        if (_currentPipeline is null) throw new InvalidOperationException("SetPipeline must be called before Draw.");
        _list->DrawInstanced(vertexCount, instanceCount, firstVertex, firstInstance);
    }

    /// <summary>Nothing to release: the underlying list and allocator are owned by the device and reused.</summary>
    public void Dispose() { }
}
