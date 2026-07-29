using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;

namespace KirasaEngine.MGL.Rendering.Backends.Direct3D12;

/// <summary>
/// Small shared helpers for the Direct3D12 backend: HRESULT checking, the Win32 event primitives the
/// fence-based CPU/GPU sync needs, and the bump descriptor allocator every heap in the device uses.
/// Mirrors <c>VulkanUtil</c>/<c>GLFormats</c> in spirit — one place for the boilerplate.
/// </summary>
internal static class D3D12Util
{
    /// <summary>D3D12_TEXTURE_DATA_PITCH_ALIGNMENT — every readback/upload row pitch is a multiple of this.</summary>
    public const uint TextureDataPitchAlignment = 256;

    /// <summary>D3D12_TEXTURE_DATA_PLACEMENT_ALIGNMENT — placed-footprint offsets must be a multiple of this.</summary>
    public const uint TextureDataPlacementAlignment = 512;

    /// <summary>D3D12_CONSTANT_BUFFER_DATA_PLACEMENT_ALIGNMENT — root CBV addresses must be 256-byte aligned.</summary>
    public const uint ConstantBufferAlignment = 256;

    /// <summary>D3D12_RESOURCE_BARRIER_ALL_SUBRESOURCES.</summary>
    public const uint AllSubresources = 0xFFFFFFFF;

    public static void Check(int hr, string what)
    {
        if (hr < 0)
            throw new InvalidOperationException($"Direct3D12 call failed ({what}): HRESULT 0x{hr:X8}");
    }

    public static uint Align(uint value, uint alignment) => (value + alignment - 1) & ~(alignment - 1);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern nint CreateEventW(nint lpEventAttributes, [MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)] bool bManualReset, [MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)] bool bInitialState, string? lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern uint WaitForSingleObject(nint hHandle, uint dwMilliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    public static extern bool CloseHandle(nint hObject);
}

/// <summary>
/// A single descriptor heap plus a monotonically increasing bump index. This project allocates a small,
/// fixed set of descriptors (a handful of RTV/DSV/SRV/sampler slots) and never recycles them mid-run, so a
/// bump allocator is sufficient — no free list, exactly the simplification the plan calls for. Freed
/// descriptors leak their slot until the whole heap dies with the device.
/// </summary>
internal sealed unsafe class D3D12DescriptorAllocator : IDisposable
{
    private readonly uint _increment;
    private readonly uint _capacity;
    private readonly CpuDescriptorHandle _cpuStart;
    private readonly GpuDescriptorHandle _gpuStart;
    private readonly bool _shaderVisible;
    private uint _next;

    public ID3D12DescriptorHeap* Heap;
    public DescriptorHeapType Type { get; }

    public D3D12DescriptorAllocator(ID3D12Device* device, DescriptorHeapType type, uint capacity, bool shaderVisible)
    {
        Type = type;
        _capacity = capacity;
        _shaderVisible = shaderVisible;

        var desc = new DescriptorHeapDesc
        {
            Type = type,
            NumDescriptors = capacity,
            Flags = shaderVisible ? DescriptorHeapFlags.ShaderVisible : DescriptorHeapFlags.None,
            NodeMask = 0,
        };

        ID3D12DescriptorHeap* heap = null;
        D3D12Util.Check(
            device->CreateDescriptorHeap(&desc, SilkMarshal.GuidPtrOf<ID3D12DescriptorHeap>(), (void**)&heap),
            $"ID3D12Device::CreateDescriptorHeap({type})");
        Heap = heap;

        _increment = device->GetDescriptorHandleIncrementSize(type);
        _cpuStart = Heap->GetCPUDescriptorHandleForHeapStart();
        if (shaderVisible) _gpuStart = Heap->GetGPUDescriptorHandleForHeapStart();
    }

    public uint Allocate()
    {
        if (_next >= _capacity)
            throw new InvalidOperationException($"Direct3D12 {Type} descriptor heap exhausted ({_capacity} descriptors).");
        return _next++;
    }

    public CpuDescriptorHandle Cpu(uint index) => new() { Ptr = _cpuStart.Ptr + (nuint)(index * _increment) };

    public GpuDescriptorHandle Gpu(uint index)
    {
        if (!_shaderVisible)
            throw new InvalidOperationException($"Direct3D12 {Type} descriptor heap is not shader-visible; it has no GPU handles.");
        return new GpuDescriptorHandle { Ptr = _gpuStart.Ptr + index * _increment };
    }

    public void Dispose()
    {
        if (Heap is null) return;
        Heap->Release();
        Heap = null;
    }
}
