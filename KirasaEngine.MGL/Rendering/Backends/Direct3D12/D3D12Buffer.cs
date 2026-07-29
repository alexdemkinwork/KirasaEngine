using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;
using Silk.NET.DXGI;

namespace KirasaEngine.MGL.Rendering.Backends.Direct3D12;

/// <summary>
/// Two distinct D3D12 buffer strategies behind one abstraction, chosen by usage:
///
/// <para><b>Upload heap, persistently mapped</b> — for <see cref="BufferUsage.Dynamic"/> data (per-frame
/// constants, per-batch instance data) and for any buffer created without initial contents. The resource
/// lives in CPU-writable, GPU-readable memory, is mapped once at construction and never unmapped, so
/// <see cref="SetData"/> is a plain memcpy. That is entirely adequate for the small, write-once-per-frame
/// buffers this renderer uses and avoids a second copy through a DEFAULT-heap resource.</para>
///
/// <para><b>Default heap + one-time staged copy</b> — for static geometry (mesh vertex/index buffers created
/// once with initial data). The contents are staged through a temporary upload buffer, copied on the
/// device's dedicated upload command list, and waited on before the constructor returns, so the resource is
/// immediately usable in its final read state.</para>
///
/// <para>Note on ordering: because a mapped upload buffer is written at *record* time rather than execute
/// time, several UpdateBuffer calls into the same buffer within one command list would all collapse to the
/// last value. SceneRenderer never does that — each frame writes each buffer once (draw-constant buffers are
/// keyed per material, instance buffers per batch) — but a future multi-write caller would need per-draw
/// suballocation instead.</para>
/// </summary>
internal sealed unsafe class D3D12Buffer : IBuffer
{
    private readonly bool _uploadHeap;
    private readonly uint _capacity;
    private byte* _mapped;

    public ID3D12Resource* Handle;
    public uint SizeInBytes { get; }
    public BufferUsage Usage { get; }

    /// <summary>The steady state this buffer sits in outside of its (single, construction-time) upload.</summary>
    public ResourceStates State { get; }

    public ulong GpuAddress => Handle->GetGPUVirtualAddress();

    public D3D12Buffer(D3D12GraphicsDevice device, in BufferDescription description, ReadOnlySpan<byte> initialData)
    {
        SizeInBytes = description.SizeInBytes;
        Usage = description.Usage;

        if (description.SizeInBytes == 0)
            throw new ArgumentException("Cannot create a zero-sized buffer.", nameof(description));

        // A root CBV's address must be 256-byte aligned, and the whole resource starts at a 64 KB boundary,
        // so padding the size is all that's needed to keep constant buffers legal.
        _capacity = description.Usage.HasFlag(BufferUsage.Uniform)
            ? D3D12Util.Align(description.SizeInBytes, D3D12Util.ConstantBufferAlignment)
            : description.SizeInBytes;

        var staging = description.Usage.HasFlag(BufferUsage.StagingRead);
        _uploadHeap = !staging && (description.Usage.HasFlag(BufferUsage.Dynamic) || initialData.IsEmpty);

        var heapType = staging ? HeapType.Readback : _uploadHeap ? HeapType.Upload : HeapType.Default;

        // Buffers are exempt from explicit state tracking: they always live in COMMON and are *implicitly
        // promoted* to whatever read/copy state each command needs, decaying back to COMMON at every
        // ExecuteCommandLists boundary. Hence no barriers anywhere on the buffer path — passing anything but
        // COMMON here just earns a "Ignoring InitialState" warning from the debug layer.
        State = staging ? ResourceStates.CopyDest
            : _uploadHeap ? ResourceStates.GenericRead
            : ResourceStates.Common;

        Handle = CreateBufferResource(device.NativeDevice, heapType, _capacity, State);

        if (_uploadHeap || staging)
        {
            // Tell the runtime we will not read what is already there (Begin == End == 0).
            var readRange = new Silk.NET.Direct3D12.Range { Begin = 0, End = 0 };
            void* mapped = null;
            D3D12Util.Check(Handle->Map(0, &readRange, &mapped), "ID3D12Resource::Map (upload buffer)");
            _mapped = (byte*)mapped;

            if (!initialData.IsEmpty) SetData(initialData, 0);
            return;
        }

        UploadStatic(device, initialData);
    }

    /// <summary>Stages <paramref name="initialData"/> through a temporary upload buffer into the DEFAULT heap resource.</summary>
    private void UploadStatic(D3D12GraphicsDevice device, ReadOnlySpan<byte> initialData)
    {
        var staging = CreateBufferResource(device.NativeDevice, HeapType.Upload, _capacity, ResourceStates.GenericRead);
        try
        {
            var readRange = new Silk.NET.Direct3D12.Range { Begin = 0, End = 0 };
            void* mapped = null;
            D3D12Util.Check(staging->Map(0, &readRange, &mapped), "ID3D12Resource::Map (staging buffer)");
            initialData.CopyTo(new Span<byte>(mapped, (int)_capacity));
            var written = new Silk.NET.Direct3D12.Range { Begin = 0, End = (nuint)initialData.Length };
            staging->Unmap(0, &written);

            var list = device.BeginUpload();
            // COMMON promotes to COPY_DEST for this copy and decays back to COMMON when the list finishes,
            // from where it promotes again to VERTEX_AND_CONSTANT_BUFFER/INDEX_BUFFER on first draw use — so
            // no transition barrier is needed or wanted here (see the note in the constructor).
            list->CopyBufferRegion(Handle, 0, staging, 0, _capacity);
            device.EndUploadAndWait();
        }
        finally
        {
            staging->Release();
        }
    }

    public void SetData(ReadOnlySpan<byte> data, uint destinationOffsetBytes)
    {
        if (data.IsEmpty) return;

        if (_mapped is null)
            throw new InvalidOperationException("This Direct3D12 buffer is immutable (DEFAULT heap); create it with BufferUsage.Dynamic to update it.");

        if (destinationOffsetBytes + (uint)data.Length > _capacity)
            throw new ArgumentOutOfRangeException(nameof(data), "Update would overrun the buffer.");

        data.CopyTo(new Span<byte>(_mapped + destinationOffsetBytes, (int)(_capacity - destinationOffsetBytes)));
    }

    private static ResourceStates InferReadState(BufferUsage usage) =>
        usage.HasFlag(BufferUsage.Index) ? ResourceStates.IndexBuffer : ResourceStates.VertexAndConstantBuffer;

    private static ID3D12Resource* CreateBufferResource(ID3D12Device* device, HeapType heapType, uint size, ResourceStates initialState)
    {
        var heapProperties = new HeapProperties
        {
            Type = heapType,
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
            Format = Format.FormatUnknown,
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
                initialState,
                null,
                SilkMarshal.GuidPtrOf<ID3D12Resource>(),
                (void**)&resource),
            $"ID3D12Device::CreateCommittedResource (buffer, {heapType})");
        return resource;
    }

    public void Dispose()
    {
        if (Handle is null) return;

        if (_mapped is not null)
        {
            Handle->Unmap(0, null);
            _mapped = null;
        }

        Handle->Release();
        Handle = null;
    }
}
