using System.Text;
using Silk.NET.Core.Native;
using Silk.NET.Shaderc;
using Silk.NET.Vulkan;
using ShadercApi = Silk.NET.Shaderc.Shaderc;

namespace KirasaEngine.MGL.Rendering.Backends.Vulkan;

/// <summary>
/// Compiles the backend-specific <c>VulkanGLSL/Standard.vert|frag</c> sources to SPIR-V at startup with
/// shaderc (the mirror image of D3D11/D3D12 running D3DCompile on the HLSL sources) and wraps each stage
/// in a <see cref="ShaderModule"/>.
/// </summary>
internal sealed unsafe class VulkanShaderSet : IShaderSet
{
    private static readonly Lazy<ShadercApi> Shaderc = new(ShadercApi.GetApi, isThreadSafe: true);

    private readonly VulkanContext _context;

    public string ShaderName { get; }
    public VertexLayoutDescription[] VertexLayouts { get; }
    public ShaderModule VertexModule { get; }
    public ShaderModule FragmentModule { get; }

    public VulkanShaderSet(VulkanContext context, ShaderSetDescription description)
    {
        _context = context;
        ShaderName = description.ShaderName;
        VertexLayouts = description.VertexLayouts;

        var vertexSource = Rendering.ShaderLibrary.ShaderLibrary.GetVulkanGlslSource(ShaderName, ShaderStage.Vertex);
        var fragmentSource = Rendering.ShaderLibrary.ShaderLibrary.GetVulkanGlslSource(ShaderName, ShaderStage.Fragment);

        VertexModule = CreateModule(CompileToSpirv(vertexSource, ShaderKind.GlslVertexShader, $"{ShaderName}.vert"));
        FragmentModule = CreateModule(CompileToSpirv(fragmentSource, ShaderKind.GlslFragmentShader, $"{ShaderName}.frag"));
    }

    private ShaderModule CreateModule(byte[] spirv)
    {
        fixed (byte* code = spirv)
        {
            var createInfo = new ShaderModuleCreateInfo
            {
                SType = StructureType.ShaderModuleCreateInfo,
                CodeSize = (nuint)spirv.Length,
                PCode = (uint*)code,
            };

            ShaderModule module;
            VulkanUtil.Check(_context.Vk.CreateShaderModule(_context.Device, &createInfo, null, &module), "vkCreateShaderModule");
            return module;
        }
    }

    private static byte[] CompileToSpirv(string source, ShaderKind kind, string sourceName)
    {
        var shaderc = Shaderc.Value;
        var compiler = shaderc.CompilerInitialize();
        if (compiler is null) throw new InvalidOperationException("shaderc_compiler_initialize returned null (is shaderc_shared native library present?).");

        var options = shaderc.CompileOptionsInitialize();
        var namePtr = SilkMarshal.StringToPtr(sourceName);
        var entryPtr = SilkMarshal.StringToPtr("main");

        try
        {
            var sourceBytes = Encoding.UTF8.GetBytes(source);
            CompilationResult* result;
            fixed (byte* pSource = sourceBytes)
            {
                result = shaderc.CompileIntoSpv(
                    compiler, pSource, (nuint)sourceBytes.Length, kind,
                    (byte*)namePtr, (byte*)entryPtr, options);
            }

            if (result is null) throw new InvalidOperationException($"shaderc returned no result for '{sourceName}'.");

            try
            {
                // The status enum is generated anonymously by Silk.NET; 0 == shaderc_compilation_status_success.
                if ((int)shaderc.ResultGetCompilationStatus(result) != 0)
                {
                    var message = shaderc.ResultGetErrorMessageS(result);
                    throw new InvalidOperationException($"shaderc failed to compile '{sourceName}': {message}");
                }

                var length = (int)shaderc.ResultGetLength(result);
                var bytes = shaderc.ResultGetBytes(result);
                var spirv = new byte[length];
                new ReadOnlySpan<byte>(bytes, length).CopyTo(spirv);
                return spirv;
            }
            finally
            {
                shaderc.ResultRelease(result);
            }
        }
        finally
        {
            SilkMarshal.Free(entryPtr);
            SilkMarshal.Free(namePtr);
            if (options is not null) shaderc.CompileOptionsRelease(options);
            shaderc.CompilerRelease(compiler);
        }
    }

    public void Dispose()
    {
        _context.Vk.DestroyShaderModule(_context.Device, VertexModule, null);
        _context.Vk.DestroyShaderModule(_context.Device, FragmentModule, null);
    }
}
