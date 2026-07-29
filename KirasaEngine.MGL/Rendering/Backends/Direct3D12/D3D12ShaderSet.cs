using System.Text;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D.Compilers;

namespace KirasaEngine.MGL.Rendering.Backends.Direct3D12;

/// <summary>
/// Compiles the shared HLSL source (the same single file, VSMain/PSMain, that the D3D11 backend uses) at
/// runtime via D3DCompile. Shader model 5_0 is deliberate: D3D12 accepts DXBC just fine, so there is no need
/// to drag in DXC/SM6 and the two D3D backends stay byte-for-byte comparable.
///
/// <para>Both blobs are retained for the lifetime of the shader set: <see cref="D3D12Pipeline"/> feeds them
/// straight into the PSO description, and the vertex blob additionally backs the input-layout validation the
/// PSO performs against the VS input signature.</para>
/// </summary>
internal sealed unsafe class D3D12ShaderSet : IShaderSet
{
    private const string VertexTarget = "vs_5_0";
    private const string PixelTarget = "ps_5_0";

    // D3DCOMPILE_ENABLE_STRICTNESS | D3DCOMPILE_OPTIMIZATION_LEVEL3
    private const uint CompileFlags = (1 << 11) | (1 << 14) | (1 << 15);

    private static readonly D3DCompiler Compiler = D3DCompiler.GetApi();

    public string ShaderName { get; }
    public VertexLayoutDescription[] VertexLayouts { get; }

    public ID3D10Blob* VertexShaderBlob;
    public ID3D10Blob* PixelShaderBlob;

    public D3D12ShaderSet(ShaderSetDescription description)
    {
        ShaderName = description.ShaderName;
        VertexLayouts = description.VertexLayouts;

        var source = Rendering.ShaderLibrary.ShaderLibrary.GetHlslSource(description.ShaderName);
        var sourceBytes = Encoding.UTF8.GetBytes(source);

        var vertexBlob = CompileStage(sourceBytes, Rendering.ShaderLibrary.ShaderLibrary.HlslVertexEntryPoint, VertexTarget);
        try
        {
            PixelShaderBlob = CompileStage(sourceBytes, Rendering.ShaderLibrary.ShaderLibrary.HlslFragmentEntryPoint, PixelTarget);
            VertexShaderBlob = vertexBlob;
            vertexBlob = null;
        }
        finally
        {
            if (vertexBlob is not null) vertexBlob->Release();
        }
    }

    private ID3D10Blob* CompileStage(byte[] source, string entryPoint, string target)
    {
        ID3D10Blob* code = null;
        ID3D10Blob* errors = null;

        int hr;
        fixed (byte* src = source)
        {
            hr = Compiler.Compile(
                src,
                (nuint)source.Length,
                $"{ShaderName}.hlsl",
                null,
                (ID3DInclude*)null,
                entryPoint,
                target,
                CompileFlags,
                0,
                ref code,
                ref errors);
        }

        if (hr < 0 || code is null)
        {
            var message = errors is not null
                ? SilkMarshal.PtrToString((nint)errors->GetBufferPointer(), NativeStringEncoding.UTF8)
                : null;
            if (errors is not null) errors->Release();
            if (code is not null) code->Release();
            throw new InvalidOperationException(
                $"HLSL {target} compile failed for shader '{ShaderName}' (entry point '{entryPoint}', HRESULT 0x{hr:X8}): {message}");
        }

        if (errors is not null) errors->Release();
        return code;
    }

    public void Dispose()
    {
        if (VertexShaderBlob is not null) { VertexShaderBlob->Release(); VertexShaderBlob = null; }
        if (PixelShaderBlob is not null) { PixelShaderBlob->Release(); PixelShaderBlob = null; }
    }
}
