using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;
using Silk.NET.DXGI;

namespace KirasaEngine.MGL.Rendering.Backends.Direct3D12;

/// <summary>
/// A DEFAULT-heap ID3D12Resource plus whichever descriptors its <see cref="TextureUsage"/> calls for, taken
/// from the device's shared heaps. Unlike D3D11 there are no view *objects* to hold: a view is just a
/// descriptor written into a heap slot, so what gets stored here is the slot's handle.
///
/// <para>The texture also owns its current <see cref="ResourceStates"/>. D3D12 has no implicit state
/// tracking, so every consumer (SetRenderTarget, ReadRenderTarget) asks this object to emit the transition
/// barrier it needs via <see cref="TransitionTo"/>, which is a no-op when the state already matches.</para>
/// </summary>
internal sealed unsafe class D3D12Texture : ITexture
{
    private readonly ResourceDesc _desc;

    public ID3D12Resource* Handle;

    public uint Width { get; }
    public uint Height { get; }
    public TextureFormat Format { get; }
    public TextureUsage Usage { get; }

    /// <summary>Live resource state, mutated whenever <see cref="TransitionTo"/> records a barrier.</summary>
    public ResourceStates CurrentState { get; private set; }

    public CpuDescriptorHandle RtvHandle { get; }
    public CpuDescriptorHandle DsvHandle { get; }
    public GpuDescriptorHandle SrvGpuHandle { get; }

    public bool HasRtv { get; }
    public bool HasDsv { get; }
    public bool HasSrv { get; }

    /// <summary>The exact resource description, needed verbatim by <c>GetCopyableFootprints</c> on readback.</summary>
    public ResourceDesc NativeDescription => _desc;

    public D3D12Texture(D3D12GraphicsDevice device, in TextureDescription description, ReadOnlySpan<byte> initialData)
    {
        Width = description.Width;
        Height = description.Height;
        Format = description.Format;
        Usage = description.Usage;

        var mipLevels = description.MipLevels == 0 ? (ushort)1 : (ushort)description.MipLevels;
        var nativeDevice = device.NativeDevice;

        var flags = ResourceFlags.None;
        if (description.Usage.HasFlag(TextureUsage.RenderTarget)) flags |= ResourceFlags.AllowRenderTarget;
        if (description.Usage.HasFlag(TextureUsage.DepthStencil))
        {
            flags |= ResourceFlags.AllowDepthStencil;
            // A depth resource that is never sampled should say so; it lets the driver pick a better layout.
            if (!description.Usage.HasFlag(TextureUsage.Sampled)) flags |= ResourceFlags.DenyShaderResource;
        }

        _desc = new ResourceDesc
        {
            Dimension = ResourceDimension.Texture2D,
            Alignment = 0,
            Width = Width,
            Height = Height,
            DepthOrArraySize = 1,
            MipLevels = mipLevels,
            Format = D3D12Formats.MapResource(description.Format, description.Usage),
            SampleDesc = new SampleDesc(1, 0),
            Layout = TextureLayout.LayoutUnknown,
            Flags = flags,
        };

        // Start in the state the texture will spend most of its life in, so the common path records no
        // barrier at all. Textures that need uploading start in COPY_DEST instead.
        CurrentState = !initialData.IsEmpty ? ResourceStates.CopyDest
            : description.Usage.HasFlag(TextureUsage.RenderTarget) ? ResourceStates.RenderTarget
            : description.Usage.HasFlag(TextureUsage.DepthStencil) ? ResourceStates.DepthWrite
            : ResourceStates.Common;

        var desc = _desc;
        ID3D12Resource* handle = null;
        var heapProperties = new HeapProperties
        {
            Type = HeapType.Default,
            CPUPageProperty = CpuPageProperty.Unknown,
            MemoryPoolPreference = MemoryPool.Unknown,
            CreationNodeMask = 1,
            VisibleNodeMask = 1,
        };

        D3D12Util.Check(
            nativeDevice->CreateCommittedResource(
                &heapProperties,
                HeapFlags.None,
                &desc,
                CurrentState,
                // No optimised clear value: the abstraction lets callers clear to any colour, and supplying a
                // mismatched hint is what actually trips the debug layer.
                null,
                SilkMarshal.GuidPtrOf<ID3D12Resource>(),
                (void**)&handle),
            "ID3D12Device::CreateCommittedResource (texture)");
        Handle = handle;

        if (!initialData.IsEmpty)
        {
            Upload(device, initialData);
            TransitionImmediate(device, ResourceStates.PixelShaderResource);
        }

        if (description.Usage.HasFlag(TextureUsage.RenderTarget))
        {
            var index = device.RtvHeap.Allocate();
            RtvHandle = device.RtvHeap.Cpu(index);

            var rtvDesc = new RenderTargetViewDesc
            {
                Format = D3D12Formats.MapRtv(description.Format),
                ViewDimension = RtvDimension.Texture2D,
            };
            rtvDesc.Anonymous.Texture2D = new Tex2DRtv { MipSlice = 0, PlaneSlice = 0 };
            nativeDevice->CreateRenderTargetView(Handle, &rtvDesc, RtvHandle);
            HasRtv = true;
        }

        if (description.Usage.HasFlag(TextureUsage.DepthStencil))
        {
            var index = device.DsvHeap.Allocate();
            DsvHandle = device.DsvHeap.Cpu(index);

            var dsvDesc = new DepthStencilViewDesc
            {
                Format = D3D12Formats.MapDsv(description.Format),
                ViewDimension = DsvDimension.Texture2D,
                Flags = DsvFlags.None,
            };
            dsvDesc.Anonymous.Texture2D = new Tex2DDsv { MipSlice = 0 };
            nativeDevice->CreateDepthStencilView(Handle, &dsvDesc, DsvHandle);
            HasDsv = true;
        }

        if (description.Usage.HasFlag(TextureUsage.Sampled))
        {
            var index = device.SrvHeap.Allocate();
            var cpu = device.SrvHeap.Cpu(index);
            SrvGpuHandle = device.SrvHeap.Gpu(index);

            var srvDesc = new ShaderResourceViewDesc
            {
                Format = D3D12Formats.MapSrv(description.Format),
                ViewDimension = SrvDimension.Texture2D,
                // D3D12_DEFAULT_SHADER_4_COMPONENT_MAPPING — identity RGBA swizzle. D3D11 defaulted to this
                // implicitly; D3D12 requires it spelled out or the SRV is invalid.
                Shader4ComponentMapping = 0x1688,
            };
            srvDesc.Anonymous.Texture2D = new Tex2DSrv
            {
                MostDetailedMip = 0,
                MipLevels = mipLevels,
                PlaneSlice = 0,
                ResourceMinLODClamp = 0f,
            };
            nativeDevice->CreateShaderResourceView(Handle, &srvDesc, cpu);
            HasSrv = true;
        }
    }

    /// <summary>Copies <paramref name="data"/> into subresource 0 through a temporary upload buffer.</summary>
    private void Upload(D3D12GraphicsDevice device, ReadOnlySpan<byte> data)
    {
        var resourceDesc = _desc;
        var footprint = new PlacedSubresourceFootprint();
        uint numRows;
        ulong rowSizeInBytes;
        ulong totalBytes;
        device.NativeDevice->GetCopyableFootprints(&resourceDesc, 0, 1, 0, &footprint, &numRows, &rowSizeInBytes, &totalBytes);

        var staging = CreateUploadBuffer(device.NativeDevice, totalBytes);
        try
        {
            var readRange = new Silk.NET.Direct3D12.Range { Begin = 0, End = 0 };
            void* mapped = null;
            D3D12Util.Check(staging->Map(0, &readRange, &mapped), "ID3D12Resource::Map (texture staging)");

            // Source rows are tightly packed; destination rows are padded to RowPitch (a multiple of 256).
            var sourcePitch = (int)(Width * D3D12Formats.BytesPerPixel(Format));
            var rowPitch = footprint.Footprint.RowPitch;
            for (var y = 0u; y < numRows; y++)
            {
                var sourceOffset = (int)(y * (uint)sourcePitch);
                if (sourceOffset >= data.Length) break;
                var length = Math.Min(sourcePitch, data.Length - sourceOffset);
                data.Slice(sourceOffset, length).CopyTo(new Span<byte>((byte*)mapped + y * rowPitch, length));
            }

            var written = new Silk.NET.Direct3D12.Range { Begin = 0, End = (nuint)totalBytes };
            staging->Unmap(0, &written);

            var list = device.BeginUpload();

            var dst = new TextureCopyLocation { PResource = Handle, Type = TextureCopyType.SubresourceIndex };
            dst.Anonymous.SubresourceIndex = 0;

            var src = new TextureCopyLocation { PResource = staging, Type = TextureCopyType.PlacedFootprint };
            src.Anonymous.PlacedFootprint = footprint;

            list->CopyTextureRegion(&dst, 0, 0, 0, &src, null);
            device.EndUploadAndWait();
        }
        finally
        {
            staging->Release();
        }
    }

    /// <summary>Records a transition barrier onto <paramref name="list"/>; no-op when already in that state.</summary>
    public void TransitionTo(ID3D12GraphicsCommandList* list, ResourceStates state)
    {
        if (CurrentState == state) return;

        var barrier = new ResourceBarrier { Type = ResourceBarrierType.Transition, Flags = ResourceBarrierFlags.None };
        barrier.Anonymous.Transition = new ResourceTransitionBarrier
        {
            PResource = Handle,
            Subresource = D3D12Util.AllSubresources,
            StateBefore = CurrentState,
            StateAfter = state,
        };
        list->ResourceBarrier(1, &barrier);
        CurrentState = state;
    }

    /// <summary>Transitions on the device's upload list and blocks — used during construction only.</summary>
    private void TransitionImmediate(D3D12GraphicsDevice device, ResourceStates state)
    {
        if (CurrentState == state) return;
        var list = device.BeginUpload();
        TransitionTo(list, state);
        device.EndUploadAndWait();
    }

    private static ID3D12Resource* CreateUploadBuffer(ID3D12Device* device, ulong size)
    {
        var heapProperties = new HeapProperties
        {
            Type = HeapType.Upload,
            CPUPageProperty = CpuPageProperty.Unknown,
            MemoryPoolPreference = MemoryPool.Unknown,
            CreationNodeMask = 1,
            VisibleNodeMask = 1,
        };

        var desc = new ResourceDesc
        {
            Dimension = ResourceDimension.Buffer,
            Alignment = 0,
            Width = size,
            Height = 1,
            DepthOrArraySize = 1,
            MipLevels = 1,
            Format = Silk.NET.DXGI.Format.FormatUnknown,
            SampleDesc = new SampleDesc(1, 0),
            Layout = TextureLayout.LayoutRowMajor,
            Flags = ResourceFlags.None,
        };

        ID3D12Resource* resource = null;
        D3D12Util.Check(
            device->CreateCommittedResource(
                &heapProperties,
                HeapFlags.None,
                &desc,
                ResourceStates.GenericRead,
                null,
                SilkMarshal.GuidPtrOf<ID3D12Resource>(),
                (void**)&resource),
            "ID3D12Device::CreateCommittedResource (texture upload)");
        return resource;
    }

    public void Dispose()
    {
        // Descriptor slots are intentionally not reclaimed — see D3D12DescriptorAllocator's doc comment.
        if (Handle is null) return;
        Handle->Release();
        Handle = null;
    }
}
