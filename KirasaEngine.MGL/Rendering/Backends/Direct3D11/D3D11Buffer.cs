using Silk.NET.Direct3D11;

namespace KirasaEngine.MGL.Rendering.Backends.Direct3D11;

internal sealed unsafe class D3D11Buffer : IBuffer
{
    private readonly ID3D11DeviceContext* _context;
    private readonly bool _dynamic;
    private readonly uint _byteWidth;

    /// <summary>
    /// CPU mirror of a dynamic buffer's contents. Map(WRITE_DISCARD) — the only efficient way to feed a
    /// D3D11 dynamic buffer — invalidates whatever was there before, so partial updates
    /// (<c>destinationOffsetBytes != 0</c>) can only be honoured by re-uploading the whole thing.
    /// </summary>
    private readonly byte[]? _shadow;

    public ID3D11Buffer* Handle;
    public uint SizeInBytes { get; }
    public BufferUsage Usage { get; }

    public D3D11Buffer(ID3D11Device* device, ID3D11DeviceContext* context, in BufferDescription description, ReadOnlySpan<byte> initialData)
    {
        _context = context;
        SizeInBytes = description.SizeInBytes;
        Usage = description.Usage;
        _dynamic = description.Usage.HasFlag(BufferUsage.Dynamic);

        // Constant buffers must be a multiple of 16 bytes wide; the abstraction's sizes already are, but
        // rounding up keeps the backend robust for callers that don't pad.
        _byteWidth = description.Usage.HasFlag(BufferUsage.Uniform)
            ? (description.SizeInBytes + 15u) & ~15u
            : description.SizeInBytes;

        if (_byteWidth == 0)
            throw new ArgumentException("Cannot create a zero-sized buffer.", nameof(description));

        var staging = description.Usage.HasFlag(BufferUsage.StagingRead);

        var desc = new BufferDesc
        {
            ByteWidth = _byteWidth,
            Usage = staging ? Silk.NET.Direct3D11.Usage.Staging
                : _dynamic ? Silk.NET.Direct3D11.Usage.Dynamic
                : Silk.NET.Direct3D11.Usage.Default,
            BindFlags = staging ? 0u : InferBindFlags(description.Usage),
            CPUAccessFlags = staging ? (uint)CpuAccessFlag.Read
                : _dynamic ? (uint)CpuAccessFlag.Write
                : 0u,
            MiscFlags = 0,
            StructureByteStride = 0,
        };

        ID3D11Buffer* handle = null;
        if (!initialData.IsEmpty)
        {
            fixed (byte* ptr = initialData)
            {
                var subresource = new SubresourceData { PSysMem = ptr, SysMemPitch = 0, SysMemSlicePitch = 0 };
                D3D11GraphicsDevice.Check(device->CreateBuffer(&desc, &subresource, ref handle), "ID3D11Device::CreateBuffer");
            }
        }
        else
        {
            D3D11GraphicsDevice.Check(device->CreateBuffer(&desc, null, ref handle), "ID3D11Device::CreateBuffer");
        }

        Handle = handle;

        if (_dynamic)
        {
            _shadow = new byte[_byteWidth];
            if (!initialData.IsEmpty) initialData.CopyTo(_shadow);
        }
    }

    public void SetData(ReadOnlySpan<byte> data, uint destinationOffsetBytes)
    {
        if (data.IsEmpty) return;
        if (destinationOffsetBytes + (uint)data.Length > _byteWidth)
            throw new ArgumentOutOfRangeException(nameof(data), "Update would overrun the buffer.");

        if (_dynamic)
        {
            data.CopyTo(_shadow.AsSpan((int)destinationOffsetBytes));

            var mapped = new MappedSubresource();
            D3D11GraphicsDevice.Check(
                _context->Map((ID3D11Resource*)Handle, 0, Map.WriteDiscard, 0, &mapped),
                "ID3D11DeviceContext::Map(WRITE_DISCARD)");
            _shadow.AsSpan().CopyTo(new Span<byte>(mapped.PData, (int)_byteWidth));
            _context->Unmap((ID3D11Resource*)Handle, 0);
            return;
        }

        fixed (byte* ptr = data)
        {
            if (Usage.HasFlag(BufferUsage.Uniform))
            {
                // D3D11.0 only allows whole-resource updates of constant buffers.
                _context->UpdateSubresource((ID3D11Resource*)Handle, 0, null, ptr, 0, 0);
            }
            else
            {
                var box = new Box
                {
                    Left = destinationOffsetBytes,
                    Right = destinationOffsetBytes + (uint)data.Length,
                    Top = 0,
                    Bottom = 1,
                    Front = 0,
                    Back = 1,
                };
                _context->UpdateSubresource((ID3D11Resource*)Handle, 0, &box, ptr, 0, 0);
            }
        }
    }

    private static uint InferBindFlags(BufferUsage usage)
    {
        // D3D11 forbids mixing CONSTANT_BUFFER with any other bind flag, and the abstraction never asks for it.
        if (usage.HasFlag(BufferUsage.Uniform)) return (uint)BindFlag.ConstantBuffer;

        var flags = 0u;
        if (usage.HasFlag(BufferUsage.Vertex)) flags |= (uint)BindFlag.VertexBuffer;
        if (usage.HasFlag(BufferUsage.Index)) flags |= (uint)BindFlag.IndexBuffer;
        if (usage.HasFlag(BufferUsage.Structured)) flags |= (uint)BindFlag.ShaderResource;
        return flags == 0 ? (uint)BindFlag.VertexBuffer : flags;
    }

    public void Dispose()
    {
        if (Handle is null) return;
        Handle->Release();
        Handle = null;
    }
}
