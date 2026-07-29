using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using VkBuffer = Silk.NET.Vulkan.Buffer;

namespace KirasaEngine.MGL.Rendering.Backends.Vulkan;

/// <summary>
/// Offscreen-only Vulkan device: instance -> physical device -> logical device with a single graphics queue,
/// a RESET_COMMAND_BUFFER command pool and one fence used for the project-wide record/submit/wait-for-idle
/// model (see <see cref="IGraphicsDevice.Submit"/>). No surface or swap chain is created — the whole
/// rendering path targets an <see cref="IRenderTarget"/> and reads it back, so <see cref="Present"/> has
/// nothing to present.
/// </summary>
public sealed unsafe class VulkanGraphicsDevice : IGraphicsDevice
{
    private const string ValidationLayerName = "VK_LAYER_KHRONOS_validation";

    private Vk _vk = null!;
    private Instance _instance;
    private PhysicalDevice _physicalDevice;
    private Device _device;
    private Queue _graphicsQueue;
    private CommandPool _commandPool;
    private Fence _fence;

    private ExtDebugUtils? _debugUtils;
    private DebugUtilsMessengerEXT _debugMessenger;
    private PfnDebugUtilsMessengerCallbackEXT _debugCallback;

    private VulkanContext _context = null!;
    private bool _disposed;

    public GraphicsBackend Backend => GraphicsBackend.Vulkan;
    public IResourceFactory Factory { get; private set; } = null!;
    public uint Width { get; private set; }
    public uint Height { get; private set; }

    public void Initialize(GraphicsDeviceDescription description)
    {
        Width = description.Width;
        Height = description.Height;

        _vk = Vk.GetApi();

        CreateInstance(description.Debug);
        if (description.Debug) TryCreateDebugMessenger();

        var queueFamily = PickPhysicalDevice();
        CreateLogicalDevice(queueFamily);

        var poolInfo = new CommandPoolCreateInfo
        {
            SType = StructureType.CommandPoolCreateInfo,
            Flags = CommandPoolCreateFlags.ResetCommandBufferBit,
            QueueFamilyIndex = queueFamily,
        };
        fixed (CommandPool* pool = &_commandPool)
            VulkanUtil.Check(_vk.CreateCommandPool(_device, &poolInfo, null, pool), "vkCreateCommandPool");

        var fenceInfo = new FenceCreateInfo { SType = StructureType.FenceCreateInfo };
        fixed (Fence* fence = &_fence)
            VulkanUtil.Check(_vk.CreateFence(_device, &fenceInfo, null, fence), "vkCreateFence");

        _context = new VulkanContext(
            _vk, _instance, _physicalDevice, _device, _graphicsQueue, queueFamily, _commandPool, _fence,
            ChooseDepthStencilFormat());

        Factory = new VulkanResourceFactory(_context);
    }

    private void CreateInstance(bool debug)
    {
        var applicationName = SilkMarshal.StringToPtr("KirasaEngine.MGL");
        var engineName = SilkMarshal.StringToPtr("KirasaEngine");

        // 1.1 is requested because negative-height viewports (the NDC-Y fix) are core there, rather than
        // needing VK_KHR_maintenance1 to be enabled explicitly.
        var appInfo = new ApplicationInfo
        {
            SType = StructureType.ApplicationInfo,
            PApplicationName = (byte*)applicationName,
            ApplicationVersion = new Version32(1, 0, 0),
            PEngineName = (byte*)engineName,
            EngineVersion = new Version32(1, 0, 0),
            ApiVersion = new Version32(1, 1, 0),
        };

        var layers = new List<string>();
        var extensions = new List<string>();

        // Validation is best-effort: plenty of target machines have a driver but no Vulkan SDK installed,
        // and a missing layer must not stop the device from coming up.
        if (debug && IsLayerAvailable(ValidationLayerName)) layers.Add(ValidationLayerName);
        if (debug && IsInstanceExtensionAvailable(ExtDebugUtils.ExtensionName)) extensions.Add(ExtDebugUtils.ExtensionName);

        var layerPtr = layers.Count > 0 ? SilkMarshal.StringArrayToPtr(layers) : 0;
        var extensionPtr = extensions.Count > 0 ? SilkMarshal.StringArrayToPtr(extensions) : 0;

        try
        {
            var createInfo = new InstanceCreateInfo
            {
                SType = StructureType.InstanceCreateInfo,
                PApplicationInfo = &appInfo,
                EnabledLayerCount = (uint)layers.Count,
                PpEnabledLayerNames = (byte**)layerPtr,
                EnabledExtensionCount = (uint)extensions.Count,
                PpEnabledExtensionNames = (byte**)extensionPtr,
            };

            fixed (Instance* instance = &_instance)
                VulkanUtil.Check(_vk.CreateInstance(&createInfo, null, instance), "vkCreateInstance");
        }
        finally
        {
            if (extensionPtr != 0) SilkMarshal.Free(extensionPtr);
            if (layerPtr != 0) SilkMarshal.Free(layerPtr);
            SilkMarshal.Free(engineName);
            SilkMarshal.Free(applicationName);
        }
    }

    private bool IsLayerAvailable(string name)
    {
        uint count = 0;
        if (_vk.EnumerateInstanceLayerProperties(&count, null) != Result.Success || count == 0) return false;

        var properties = new LayerProperties[count];
        fixed (LayerProperties* pProperties = properties)
        {
            if (_vk.EnumerateInstanceLayerProperties(&count, pProperties) != Result.Success) return false;
            for (uint i = 0; i < count; i++)
            {
                if (SilkMarshal.PtrToString((nint)pProperties[i].LayerName) == name) return true;
            }
        }

        return false;
    }

    private bool IsInstanceExtensionAvailable(string name)
    {
        uint count = 0;
        if (_vk.EnumerateInstanceExtensionProperties((byte*)null, &count, null) != Result.Success || count == 0) return false;

        var properties = new ExtensionProperties[count];
        fixed (ExtensionProperties* pProperties = properties)
        {
            if (_vk.EnumerateInstanceExtensionProperties((byte*)null, &count, pProperties) != Result.Success) return false;
            for (uint i = 0; i < count; i++)
            {
                if (SilkMarshal.PtrToString((nint)pProperties[i].ExtensionName) == name) return true;
            }
        }

        return false;
    }

    private void TryCreateDebugMessenger()
    {
        if (!_vk.TryGetInstanceExtension(_instance, out ExtDebugUtils debugUtils)) return;
        _debugUtils = debugUtils;
        _debugCallback = new PfnDebugUtilsMessengerCallbackEXT(DebugCallback);

        var createInfo = new DebugUtilsMessengerCreateInfoEXT
        {
            SType = StructureType.DebugUtilsMessengerCreateInfoExt,
            MessageSeverity = DebugUtilsMessageSeverityFlagsEXT.WarningBitExt | DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt,
            MessageType = DebugUtilsMessageTypeFlagsEXT.GeneralBitExt
                          | DebugUtilsMessageTypeFlagsEXT.ValidationBitExt
                          | DebugUtilsMessageTypeFlagsEXT.PerformanceBitExt,
            PfnUserCallback = _debugCallback,
        };

        fixed (DebugUtilsMessengerEXT* messenger = &_debugMessenger)
        {
            if (_debugUtils.CreateDebugUtilsMessenger(_instance, &createInfo, null, messenger) != Result.Success)
                _debugMessenger = default;
        }
    }

    private static uint DebugCallback(
        DebugUtilsMessageSeverityFlagsEXT severity,
        DebugUtilsMessageTypeFlagsEXT messageType,
        DebugUtilsMessengerCallbackDataEXT* callbackData,
        void* userData)
    {
        Console.Error.WriteLine($"[Vulkan {severity}] {SilkMarshal.PtrToString((nint)callbackData->PMessage)}");
        return Vk.False;
    }

    /// <summary>Picks the first device exposing a graphics queue family; no scoring heuristics are needed here.</summary>
    private uint PickPhysicalDevice()
    {
        uint deviceCount = 0;
        VulkanUtil.Check(_vk.EnumeratePhysicalDevices(_instance, &deviceCount, null), "vkEnumeratePhysicalDevices");
        if (deviceCount == 0)
            throw new InvalidOperationException("No Vulkan-capable physical device was found (is a Vulkan-capable GPU driver installed?).");

        var devices = new PhysicalDevice[deviceCount];
        fixed (PhysicalDevice* pDevices = devices)
            VulkanUtil.Check(_vk.EnumeratePhysicalDevices(_instance, &deviceCount, pDevices), "vkEnumeratePhysicalDevices");

        foreach (var candidate in devices)
        {
            uint familyCount = 0;
            _vk.GetPhysicalDeviceQueueFamilyProperties(candidate, &familyCount, null);
            if (familyCount == 0) continue;

            var families = new QueueFamilyProperties[familyCount];
            fixed (QueueFamilyProperties* pFamilies = families)
                _vk.GetPhysicalDeviceQueueFamilyProperties(candidate, &familyCount, pFamilies);

            for (uint i = 0; i < familyCount; i++)
            {
                if (!families[i].QueueFlags.HasFlag(QueueFlags.GraphicsBit)) continue;

                _physicalDevice = candidate;
                return i;
            }
        }

        throw new InvalidOperationException("No Vulkan physical device exposes a graphics-capable queue family.");
    }

    private void CreateLogicalDevice(uint queueFamily)
    {
        var priority = 1f;
        var queueInfo = new DeviceQueueCreateInfo
        {
            SType = StructureType.DeviceQueueCreateInfo,
            QueueFamilyIndex = queueFamily,
            QueueCount = 1,
            PQueuePriorities = &priority,
        };

        var features = new PhysicalDeviceFeatures { FillModeNonSolid = true };

        // On a 1.0-only driver the negative-height viewport needs VK_KHR_maintenance1 enabled explicitly;
        // on 1.1+ it is core and the extension is not advertised as a separate device extension anymore.
        _vk.GetPhysicalDeviceProperties(_physicalDevice, out var properties);
        var deviceExtensions = new List<string>();
        if ((uint)properties.ApiVersion < (uint)new Version32(1, 1, 0) && IsDeviceExtensionAvailable("VK_KHR_maintenance1"))
            deviceExtensions.Add("VK_KHR_maintenance1");

        var extensionPtr = deviceExtensions.Count > 0 ? SilkMarshal.StringArrayToPtr(deviceExtensions) : 0;
        try
        {
            var createInfo = new DeviceCreateInfo
            {
                SType = StructureType.DeviceCreateInfo,
                QueueCreateInfoCount = 1,
                PQueueCreateInfos = &queueInfo,
                PEnabledFeatures = &features,
                EnabledExtensionCount = (uint)deviceExtensions.Count,
                PpEnabledExtensionNames = (byte**)extensionPtr,
            };

            fixed (Device* device = &_device)
                VulkanUtil.Check(_vk.CreateDevice(_physicalDevice, &createInfo, null, device), "vkCreateDevice");
        }
        finally
        {
            if (extensionPtr != 0) SilkMarshal.Free(extensionPtr);
        }

        _vk.GetDeviceQueue(_device, queueFamily, 0, out _graphicsQueue);
    }

    private bool IsDeviceExtensionAvailable(string name)
    {
        uint count = 0;
        if (_vk.EnumerateDeviceExtensionProperties(_physicalDevice, (byte*)null, &count, null) != Result.Success || count == 0) return false;

        var properties = new ExtensionProperties[count];
        fixed (ExtensionProperties* pProperties = properties)
        {
            if (_vk.EnumerateDeviceExtensionProperties(_physicalDevice, (byte*)null, &count, pProperties) != Result.Success) return false;
            for (uint i = 0; i < count; i++)
            {
                if (SilkMarshal.PtrToString((nint)pProperties[i].ExtensionName) == name) return true;
            }
        }

        return false;
    }

    /// <summary>D24_UNORM_S8_UINT is not universal (several AMD parts only expose D32_SFLOAT_S8_UINT).</summary>
    private Format ChooseDepthStencilFormat()
    {
        foreach (var candidate in (ReadOnlySpan<Format>)[Format.D24UnormS8Uint, Format.D32SfloatS8Uint, Format.D16UnormS8Uint])
        {
            _vk.GetPhysicalDeviceFormatProperties(_physicalDevice, candidate, out var formatProperties);
            if (formatProperties.OptimalTilingFeatures.HasFlag(FormatFeatureFlags.DepthStencilAttachmentBit))
                return candidate;
        }

        throw new InvalidOperationException("No supported depth-stencil format found for TextureFormat.Depth24Stencil8.");
    }

    public ICommandList CreateCommandList() => new VulkanCommandList(_context);

    public void Submit(ICommandList commandList) =>
        _context.SubmitAndWait(((VulkanCommandList)commandList).CommandBuffer);

    /// <summary>No-op: this backend never creates a surface or swap chain (see the type's remarks).</summary>
    public void Present() { }

    /// <summary>
    /// Records the new dimensions only. Render targets are created explicitly through
    /// <see cref="IResourceFactory.CreateRenderTarget"/>, and there is no swap chain to resize.
    /// </summary>
    public void Resize(uint width, uint height)
    {
        Width = width;
        Height = height;
    }

    public byte[] ReadRenderTarget(IRenderTarget target)
    {
        var vulkanTarget = (VulkanRenderTarget)target;
        var color = vulkanTarget.Color;
        var byteCount = (int)(vulkanTarget.Width * vulkanTarget.Height * 4);
        var pixels = new byte[byteCount];

        var (staging, stagingMemory) = _context.CreateRawBuffer(
            (ulong)byteCount,
            BufferUsageFlags.TransferDstBit,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);

        try
        {
            var cmd = _context.BeginOneTimeCommands();

            _context.TransitionImageLayout(cmd, color.Handle, color.Aspect, color.CurrentLayout, ImageLayout.TransferSrcOptimal);

            var region = new BufferImageCopy
            {
                BufferOffset = 0,
                // 0 == tightly packed to the image extent, which for R8G8B8A8_UNORM is exactly
                // width*height*4 with no row padding (no D3D12-style row-pitch alignment applies here).
                BufferRowLength = 0,
                BufferImageHeight = 0,
                ImageSubresource = new ImageSubresourceLayers(ImageAspectFlags.ColorBit, 0, 0, 1),
                ImageOffset = new Offset3D(0, 0, 0),
                ImageExtent = new Extent3D(vulkanTarget.Width, vulkanTarget.Height, 1),
            };
            _vk.CmdCopyImageToBuffer(cmd, color.Handle, ImageLayout.TransferSrcOptimal, staging, 1, &region);

            _context.EndOneTimeCommands(cmd);
            color.CurrentLayout = ImageLayout.TransferSrcOptimal;

            void* mapped;
            VulkanUtil.Check(_vk.MapMemory(_device, stagingMemory, 0, (ulong)byteCount, 0, &mapped), "vkMapMemory (readback)");
            new ReadOnlySpan<byte>(mapped, byteCount).CopyTo(pixels);
            _vk.UnmapMemory(_device, stagingMemory);
        }
        finally
        {
            _vk.DestroyBuffer(_device, staging, null);
            _vk.FreeMemory(_device, stagingMemory, null);
        }

        // No vertical flip, unlike OpenGL: Vulkan image row 0 is already the top of the image, and the
        // NDC-Y inversion is dealt with by the negative-height viewport in VulkanCommandList.SetViewport.
        return pixels;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_vk is null) return;

        if (_device.Handle != 0)
        {
            _vk.DeviceWaitIdle(_device);

            if (_fence.Handle != 0) _vk.DestroyFence(_device, _fence, null);
            if (_commandPool.Handle != 0) _vk.DestroyCommandPool(_device, _commandPool, null);
            _vk.DestroyDevice(_device, null);
            _device = default;
        }

        if (_debugUtils is not null)
        {
            if (_debugMessenger.Handle != 0) _debugUtils.DestroyDebugUtilsMessenger(_instance, _debugMessenger, null);
            _debugUtils.Dispose();
            _debugUtils = null;
        }

        if (_instance.Handle != 0)
        {
            _vk.DestroyInstance(_instance, null);
            _instance = default;
        }

        _vk.Dispose();
    }
}
