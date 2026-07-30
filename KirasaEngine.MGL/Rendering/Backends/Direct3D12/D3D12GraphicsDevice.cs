using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;
using Silk.NET.DXGI;

using KirasaEngine.MGL.Rendering;

namespace KirasaEngine.MGL.Rendering.Backends.Direct3D12;

/// <summary>
/// Direct3D12 device: adapter/device/queue creation, the descriptor heaps shared by every resource, and the
/// fence-based synchronous submit model this project deliberately uses (record -> execute -> block until the
/// GPU is idle; see the doc comment on <see cref="IGraphicsDevice.Submit"/>). No frames are ever in flight,
/// which is why a single command allocator + command list can be reused forever and why resources may be
/// released the moment their owner is disposed.
/// </summary>
public sealed unsafe class D3D12GraphicsDevice : IGraphicsDevice
{
    private D3D12 _d3d12 = null!;
    private DXGI _dxgi = null!;

    private IDXGIFactory1* _factory;
    private IDXGIAdapter1* _adapter;
    private ID3D12Device* _device;
    private ID3D12CommandQueue* _queue;

    // The one reusable direct command list handed out by CreateCommandList (see D3D12CommandList).
    private ID3D12CommandAllocator* _allocator;
    private ID3D12GraphicsCommandList* _list;

    // A second allocator/list dedicated to blocking uploads and readbacks. It exists because
    // SceneRenderer creates static mesh buffers *while the frame's command list is open* — resetting or
    // executing the recording list at that point would discard the frame.
    private ID3D12CommandAllocator* _uploadAllocator;
    private ID3D12GraphicsCommandList* _uploadList;

    private ID3D12Fence* _fence;
    private ulong _fenceValue;
    private nint _fenceEvent;

    private bool _disposed;

    public GraphicsBackend Backend => GraphicsBackend.Direct3D12;
    public IResourceFactory Factory { get; private set; } = null!;
    public uint Width { get; private set; }
    public uint Height { get; private set; }

    internal ID3D12Device* NativeDevice => _device;
    internal ID3D12GraphicsCommandList* NativeCommandList => _list;
    internal ID3D12CommandAllocator* NativeCommandAllocator => _allocator;

    internal D3D12DescriptorAllocator RtvHeap { get; private set; } = null!;
    internal D3D12DescriptorAllocator DsvHeap { get; private set; } = null!;
    internal D3D12DescriptorAllocator SrvHeap { get; private set; } = null!;
    internal D3D12DescriptorAllocator SamplerHeap { get; private set; } = null!;

    public void Initialize(GraphicsDeviceDescription description)
    {
        Width = description.Width;
        Height = description.Height;

        _d3d12 = D3D12.GetApi();
        _dxgi = DXGI.GetApi(null, false);

        if (description.Debug) EnableDebugLayer();

        // CreateDXGIFactory1 (not CreateDXGIFactory) is the entry point that accepts an IDXGIFactory1 riid;
        // the DXGI 1.0 one answers E_NOINTERFACE for it. IDXGIFactory1 is what gives us EnumAdapters1 and
        // therefore the software-adapter flag needed to skip WARP.
        IDXGIFactory1* factory = null;
        D3D12Util.Check(
            _dxgi.CreateDXGIFactory1(SilkMarshal.GuidPtrOf<IDXGIFactory1>(), (void**)&factory),
            "CreateDXGIFactory1");
        _factory = factory;

        CreateDeviceOnHardwareAdapter();

        var queueDesc = new CommandQueueDesc
        {
            Type = CommandListType.Direct,
            Priority = (int)CommandQueuePriority.Normal,
            Flags = CommandQueueFlags.None,
            NodeMask = 0,
        };
        ID3D12CommandQueue* queue = null;
        D3D12Util.Check(
            _device->CreateCommandQueue(&queueDesc, SilkMarshal.GuidPtrOf<ID3D12CommandQueue>(), (void**)&queue),
            "ID3D12Device::CreateCommandQueue");
        _queue = queue;

        fixed (ID3D12CommandAllocator** allocator = &_allocator)
        fixed (ID3D12GraphicsCommandList** list = &_list)
            CreateCommandListPair(allocator, list);

        fixed (ID3D12CommandAllocator** allocator = &_uploadAllocator)
        fixed (ID3D12GraphicsCommandList** list = &_uploadList)
            CreateCommandListPair(allocator, list);

        ID3D12Fence* fence = null;
        D3D12Util.Check(
            _device->CreateFence(0, FenceFlags.None, SilkMarshal.GuidPtrOf<ID3D12Fence>(), (void**)&fence),
            "ID3D12Device::CreateFence");
        _fence = fence;

        _fenceEvent = D3D12Util.CreateEventW(0, false, false, null);
        if (_fenceEvent == 0)
            throw new InvalidOperationException("CreateEventW failed while setting up Direct3D12 fence synchronisation.");

        // Capacities are generous for this project's fixed resource set (one render target, a white
        // fallback texture, one sampler) but still tiny; the bump allocators never recycle slots.
        RtvHeap = new D3D12DescriptorAllocator(_device, DescriptorHeapType.Rtv, 64, shaderVisible: false);
        DsvHeap = new D3D12DescriptorAllocator(_device, DescriptorHeapType.Dsv, 64, shaderVisible: false);
        SrvHeap = new D3D12DescriptorAllocator(_device, DescriptorHeapType.CbvSrvUav, 1024, shaderVisible: true);
        SamplerHeap = new D3D12DescriptorAllocator(_device, DescriptorHeapType.Sampler, 64, shaderVisible: true);

        Factory = new D3D12ResourceFactory(this);
    }

    private void EnableDebugLayer()
    {
        ID3D12Debug* debug = null;
        var hr = _d3d12.GetDebugInterface(SilkMarshal.GuidPtrOf<ID3D12Debug>(), (void**)&debug);
        if (hr < 0 || debug is null) return; // Graphics Tools optional feature not installed — not fatal.
        debug->EnableDebugLayer();
        debug->Release();
    }

    private void CreateDeviceOnHardwareAdapter()
    {
        for (uint index = 0; ; index++)
        {
            IDXGIAdapter1* adapter = null;
            if (_factory->EnumAdapters1(index, &adapter) < 0 || adapter is null) break;

            var adapterDesc = new AdapterDesc1();
            if (adapter->GetDesc1(&adapterDesc) >= 0 && (adapterDesc.Flags & (uint)AdapterFlag.Software) != 0)
            {
                adapter->Release();
                continue;
            }

            ID3D12Device* device = null;
            var hr = _d3d12.CreateDevice(
                (IUnknown*)adapter,
                D3DFeatureLevel.Level120,
                SilkMarshal.GuidPtrOf<ID3D12Device>(),
                (void**)&device);

            if (hr >= 0 && device is not null)
            {
                _adapter = adapter;
                _device = device;
                return;
            }

            adapter->Release();
        }

        throw new InvalidOperationException("No Direct3D12 feature-level 12_0 hardware adapter was found.");
    }

    private void CreateCommandListPair(ID3D12CommandAllocator** outAllocator, ID3D12GraphicsCommandList** outList)
    {
        ID3D12CommandAllocator* allocator = null;
        D3D12Util.Check(
            _device->CreateCommandAllocator(CommandListType.Direct, SilkMarshal.GuidPtrOf<ID3D12CommandAllocator>(), (void**)&allocator),
            "ID3D12Device::CreateCommandAllocator");

        ID3D12GraphicsCommandList* list = null;
        D3D12Util.Check(
            _device->CreateCommandList(0, CommandListType.Direct, allocator, (ID3D12PipelineState*)null, SilkMarshal.GuidPtrOf<ID3D12GraphicsCommandList>(), (void**)&list),
            "ID3D12Device::CreateCommandList");

        // Command lists are created in the recording state; close them so the first Begin()/BeginUpload()
        // can Reset() unconditionally.
        D3D12Util.Check(list->Close(), "ID3D12GraphicsCommandList::Close");

        *outAllocator = allocator;
        *outList = list;
    }

    /// <summary>
    /// Hands out a wrapper around the device's single reusable command list. Legal because Submit() blocks
    /// until the GPU has consumed the previous recording, so the list is provably free by the time anyone
    /// asks for a new one.
    /// </summary>
    public ICommandList CreateCommandList() => new D3D12CommandList(this);

    public void Submit(ICommandList commandList)
    {
        _ = (D3D12CommandList)commandList;

        var list = (ID3D12CommandList*)_list;
        _queue->ExecuteCommandLists(1, &list);
        WaitForGpu();
    }

    /// <summary>
    /// Resets the dedicated upload/readback list and returns it for recording copies. Pair every call with
    /// <see cref="EndUploadAndWait"/>; the pair is fully blocking, so any staging resource the caller
    /// created may be released as soon as it returns.
    /// </summary>
    internal ID3D12GraphicsCommandList* BeginUpload()
    {
        D3D12Util.Check(_uploadAllocator->Reset(), "ID3D12CommandAllocator::Reset (upload)");
        D3D12Util.Check(_uploadList->Reset(_uploadAllocator, (ID3D12PipelineState*)null), "ID3D12GraphicsCommandList::Reset (upload)");
        return _uploadList;
    }

    internal void EndUploadAndWait()
    {
        D3D12Util.Check(_uploadList->Close(), "ID3D12GraphicsCommandList::Close (upload)");
        var list = (ID3D12CommandList*)_uploadList;
        _queue->ExecuteCommandLists(1, &list);
        WaitForGpu();
    }

    /// <summary>Signals the fence with a fresh value and blocks the calling thread until the GPU reaches it.</summary>
    internal void WaitForGpu()
    {
        var target = ++_fenceValue;
        D3D12Util.Check(_queue->Signal(_fence, target), "ID3D12CommandQueue::Signal");

        if (_fence->GetCompletedValue() >= target) return;

        D3D12Util.Check(_fence->SetEventOnCompletion(target, (void*)_fenceEvent), "ID3D12Fence::SetEventOnCompletion");
        D3D12Util.WaitForSingleObject(_fenceEvent, 0xFFFFFFFF);
    }

    /// <summary>
    /// No-op: this backend is headless by design. The whole render path targets an offscreen
    /// <see cref="IRenderTarget"/> that is read back with <see cref="ReadRenderTarget"/>, so no swap chain
    /// is ever created and there is nothing to flip. Kept to satisfy the interface, matching the "no-op if
    /// the device was created headless" contract.
    /// </summary>
    public void Present() { }

    /// <summary>
    /// Records the new backbuffer-equivalent size. With no swap chain there are no buffers to resize; render
    /// targets are sized explicitly by <see cref="IResourceFactory.CreateRenderTarget"/>.
    /// </summary>
    public void Resize(uint width, uint height)
    {
        Width = width;
        Height = height;
    }

    public byte[] ReadRenderTarget(IRenderTarget target)
    {
        var d3dTarget = (D3D12RenderTarget)target;
        var color = (D3D12Texture)d3dTarget.ColorTexture;

        var width = color.Width;
        var height = color.Height;
        var bytesPerPixel = D3D12Formats.BytesPerPixel(color.Format);

        // The GPU-side row pitch is padded to D3D12_TEXTURE_DATA_PITCH_ALIGNMENT (256), so it almost never
        // equals width * 4 — the readback below must copy row by row using the returned RowPitch.
        var resourceDesc = color.NativeDescription;
        var footprint = new PlacedSubresourceFootprint();
        uint numRows;
        ulong rowSizeInBytes;
        ulong totalBytes;
        _device->GetCopyableFootprints(&resourceDesc, 0, 1, 0, &footprint, &numRows, &rowSizeInBytes, &totalBytes);

        var readback = CreateReadbackBuffer(totalBytes);

        try
        {
            var list = BeginUpload();

            color.TransitionTo(list, ResourceStates.CopySource);

            var dst = new TextureCopyLocation { PResource = readback, Type = TextureCopyType.PlacedFootprint };
            dst.Anonymous.PlacedFootprint = footprint;

            var src = new TextureCopyLocation { PResource = color.Handle, Type = TextureCopyType.SubresourceIndex };
            src.Anonymous.SubresourceIndex = 0;

            list->CopyTextureRegion(&dst, 0, 0, 0, &src, null);

            // Leave the attachment ready to be drawn into again next frame; SetRenderTarget would otherwise
            // have to guess which state a previously-read target was left in.
            color.TransitionTo(list, ResourceStates.RenderTarget);

            EndUploadAndWait();

            void* mapped = null;
            var readRange = new Silk.NET.Direct3D12.Range { Begin = 0, End = (nuint)totalBytes };
            D3D12Util.Check(readback->Map(0, &readRange, &mapped), "ID3D12Resource::Map (readback)");

            var pixels = new byte[width * height * 4];
            var rowPitch = footprint.Footprint.RowPitch;
            var tightPitch = width * bytesPerPixel;
            var swizzleBgra = color.Format == TextureFormat.Bgra8UNorm;

            for (var y = 0u; y < height; y++)
            {
                var source = new ReadOnlySpan<byte>((byte*)mapped + y * rowPitch, (int)tightPitch);
                var destination = pixels.AsSpan((int)(y * tightPitch), (int)tightPitch);
                source.CopyTo(destination);

                // The abstraction always hands back RGBA8; a BGRA target needs its channels swapped.
                if (!swizzleBgra) continue;
                for (var x = 0; x < destination.Length; x += 4)
                    (destination[x], destination[x + 2]) = (destination[x + 2], destination[x]);
            }

            var written = new Silk.NET.Direct3D12.Range { Begin = 0, End = 0 };
            readback->Unmap(0, &written);

            // D3D texture row 0 is already the top of the image — unlike OpenGL, no vertical flip here.
            return pixels;
        }
        finally
        {
            readback->Release();
        }
    }

    private ID3D12Resource* CreateReadbackBuffer(ulong sizeInBytes)
    {
        var heapProperties = new HeapProperties
        {
            Type = HeapType.Readback,
            CPUPageProperty = CpuPageProperty.Unknown,
            MemoryPoolPreference = MemoryPool.Unknown,
            CreationNodeMask = 1,
            VisibleNodeMask = 1,
        };

        var desc = new ResourceDesc
        {
            Dimension = ResourceDimension.Buffer,
            Alignment = 0,
            Width = sizeInBytes,
            Height = 1,
            DepthOrArraySize = 1,
            MipLevels = 1,
            Format = Format.FormatUnknown,
            SampleDesc = new SampleDesc(1, 0),
            Layout = TextureLayout.LayoutRowMajor,
            Flags = ResourceFlags.None,
        };

        ID3D12Resource* resource = null;
        D3D12Util.Check(
            _device->CreateCommittedResource(
                &heapProperties,
                HeapFlags.None,
                &desc,
                ResourceStates.CopyDest,
                null,
                SilkMarshal.GuidPtrOf<ID3D12Resource>(),
                (void**)&resource),
            "ID3D12Device::CreateCommittedResource (readback)");
        return resource;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // D3D12 forbids releasing anything the GPU might still be reading. Every Submit()/EndUploadAndWait()
        // already blocked, but flush once more in case a caller disposed the device mid-recording.
        if (_queue is not null && _fence is not null) WaitForGpu();

        SamplerHeap?.Dispose();
        SrvHeap?.Dispose();
        DsvHeap?.Dispose();
        RtvHeap?.Dispose();

        if (_fenceEvent != 0) { D3D12Util.CloseHandle(_fenceEvent); _fenceEvent = 0; }
        if (_fence is not null) { _fence->Release(); _fence = null; }
        if (_uploadList is not null) { _uploadList->Release(); _uploadList = null; }
        if (_uploadAllocator is not null) { _uploadAllocator->Release(); _uploadAllocator = null; }
        if (_list is not null) { _list->Release(); _list = null; }
        if (_allocator is not null) { _allocator->Release(); _allocator = null; }
        if (_queue is not null) { _queue->Release(); _queue = null; }
        if (_device is not null) { _device->Release(); _device = null; }
        if (_adapter is not null) { _adapter->Release(); _adapter = null; }
        if (_factory is not null) { _factory->Release(); _factory = null; }

        _dxgi?.Dispose();
        _d3d12?.Dispose();
    }
}
