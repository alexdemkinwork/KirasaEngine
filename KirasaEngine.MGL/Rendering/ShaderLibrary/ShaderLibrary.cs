using System.Reflection;

namespace KirasaEngine.MGL.Rendering.ShaderLibrary;

/// <summary>
/// Loads embedded per-backend shader sources by logical name (e.g. "Standard"). GLSL/VulkanGLSL text is
/// consumed directly by the OpenGL/Vulkan backends (Vulkan compiles it to SPIR-V at runtime via Shaderc);
/// HLSL text is compiled at runtime by the D3D11/D3D12 backends via D3DCompile.
/// </summary>
public static class ShaderLibrary
{
    public const string HlslVertexEntryPoint = "VSMain";
    public const string HlslFragmentEntryPoint = "PSMain";

    private static readonly Assembly ThisAssembly = typeof(ShaderLibrary).Assembly;

    public static string GetGlslSource(string shaderName, ShaderStage stage) =>
        GetResourceText($"GLSL.{shaderName}{GlslExtension(stage)}");

    public static string GetVulkanGlslSource(string shaderName, ShaderStage stage) =>
        GetResourceText($"VulkanGLSL.{shaderName}{GlslExtension(stage)}");

    public static string GetHlslSource(string shaderName) =>
        GetResourceText($"HLSL.{shaderName}.hlsl");

    private static string GlslExtension(ShaderStage stage) => stage switch
    {
        ShaderStage.Vertex => ".vert",
        ShaderStage.Fragment => ".frag",
        _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, "Expected a single shader stage."),
    };

    private static string GetResourceText(string suffix)
    {
        var fullSuffix = "Rendering.ShaderLibrary." + suffix;
        var resourceName = ThisAssembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(fullSuffix, StringComparison.Ordinal));

        if (resourceName is null)
            throw new InvalidOperationException($"Embedded shader resource not found (expected suffix '*.{fullSuffix}').");

        using var stream = ThisAssembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
