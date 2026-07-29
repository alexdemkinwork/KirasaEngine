using Silk.NET.Vulkan;

namespace KirasaEngine.MGL.Rendering.Backends.Vulkan;

internal static class VulkanUtil
{
    public static void Check(Result result, string what)
    {
        if (result != Result.Success)
            throw new InvalidOperationException($"Vulkan call failed ({what}): {result}");
    }
}
