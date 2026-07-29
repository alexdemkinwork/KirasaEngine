using Silk.NET.Vulkan;
using VkBuffer = Silk.NET.Vulkan.Buffer;

namespace KirasaEngine.MGL.Rendering.Backends.Vulkan;

/// <summary>
/// Every buffer — vertex, index, uniform, dynamic or not — is backed by HOST_VISIBLE | HOST_COHERENT memory
/// and stays mapped for the lifetime of the object, so <see cref="SetData"/> is a plain memcpy with no flush
/// and no staging round-trip.
/// </summary>
/// <remarks>
/// The device-local + staging-copy path would be faster for the static mesh buffers, but this project's
/// stated priority is a correct, simple backend (same reasoning as the synchronous submit model), and the
/// smoke scene's buffers total a few hundred kilobytes. Coherent memory removes the vkFlushMappedMemoryRanges
/// bookkeeping entirely.
/// </remarks>
internal sealed unsafe class VulkanBuffer : IBuffer
{
    private readonly VulkanContext _context;
    private readonly void* _mapped;

    public VkBuffer Handle { get; }
    public DeviceMemory Memory { get; }
    public uint SizeInBytes { get; }
    public BufferUsage Usage { get; }

    public VulkanBuffer(VulkanContext context, in BufferDescription description, ReadOnlySpan<byte> initialData)
    {
        _context = context;
        SizeInBytes = description.SizeInBytes;
        Usage = description.Usage;

        (Handle, Memory) = context.CreateRawBuffer(
            SizeInBytes,
            InferUsageFlags(description.Usage),
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);

        void* mapped;
        VulkanUtil.Check(context.Vk.MapMemory(context.Device, Memory, 0, Vk.WholeSize, 0, &mapped), "vkMapMemory");
        _mapped = mapped;

        if (!initialData.IsEmpty) SetData(initialData, 0);
    }

    public void SetData(ReadOnlySpan<byte> data, uint destinationOffsetBytes)
    {
        if (data.IsEmpty) return;
        if (destinationOffsetBytes + (uint)data.Length > SizeInBytes)
            throw new ArgumentOutOfRangeException(nameof(data), $"Upload of {data.Length} bytes at offset {destinationOffsetBytes} exceeds the {SizeInBytes}-byte buffer.");

        var destination = new Span<byte>((byte*)_mapped + destinationOffsetBytes, (int)(SizeInBytes - destinationOffsetBytes));
        data.CopyTo(destination);
    }

    private static BufferUsageFlags InferUsageFlags(BufferUsage usage)
    {
        // TransferSrc/Dst are always allowed so any buffer can participate in a copy without a second type.
        var flags = BufferUsageFlags.TransferSrcBit | BufferUsageFlags.TransferDstBit;
        if (usage.HasFlag(BufferUsage.Vertex)) flags |= BufferUsageFlags.VertexBufferBit;
        if (usage.HasFlag(BufferUsage.Index)) flags |= BufferUsageFlags.IndexBufferBit;
        if (usage.HasFlag(BufferUsage.Uniform)) flags |= BufferUsageFlags.UniformBufferBit;
        if (usage.HasFlag(BufferUsage.Structured)) flags |= BufferUsageFlags.StorageBufferBit;
        return flags;
    }

    public void Dispose()
    {
        var vk = _context.Vk;
        vk.UnmapMemory(_context.Device, Memory);
        vk.DestroyBuffer(_context.Device, Handle, null);
        vk.FreeMemory(_context.Device, Memory, null);
    }
}
