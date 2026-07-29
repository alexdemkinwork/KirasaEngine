using System.Text;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D.Compilers;
using Silk.NET.Direct3D11;

namespace KirasaEngine.MGL.Rendering.Backends.Direct3D11;

/// <summary>
/// Compiles the shared HLSL source (one file, VSMain/PSMain entry points) at runtime via D3DCompile.
/// The compiled vertex-shader blob is retained because <see cref="D3D11Pipeline"/> needs its input
/// signature to build the ID3D11InputLayout.
/// </summary>
internal sealed unsafe class D3D11ShaderSet : IShaderSet
{
    private const string VertexTarget = "vs_5_0";
    private const string PixelTarget = "ps_5_0";

    // D3DCOMPILE_ENABLE_STRICTNESS | D3DCOMPILE_OPTIMIZATION_LEVEL3
    private const uint CompileFlags = (1 << 11) | (1 << 14) | (1 << 15);

    private static readonly D3DCompiler Compiler = D3DCompiler.GetApi();

    public string ShaderName { get; }
    public VertexLayoutDescription[] VertexLayouts { get; }

    public ID3D11VertexShader* VertexShader;
    public ID3D11PixelShader* PixelShader;

    /// <summary>Kept alive for <c>ID3D11Device::CreateInputLayout</c>, which validates against the VS input signature.</summary>
    public ID3D10Blob* VertexShaderBlob;

    public D3D11ShaderSet(ID3D11Device* device, ShaderSetDescription description)
    {
        ShaderName = description.ShaderName;
        VertexLayouts = description.VertexLayouts;

        var source = Rendering.ShaderLibrary.ShaderLibrary.GetHlslSource(description.ShaderName);
        var sourceBytes = Encoding.UTF8.GetBytes(source);

        var vertexBlob = CompileStage(sourceBytes, Rendering.ShaderLibrary.ShaderLibrary.HlslVertexEntryPoint, VertexTarget);
        ID3D10Blob* pixelBlob = null;

        try
        {
            pixelBlob = CompileStage(sourceBytes, Rendering.ShaderLibrary.ShaderLibrary.HlslFragmentEntryPoint, PixelTarget);

            ID3D11VertexShader* vs = null;
            D3D11GraphicsDevice.Check(
                device->CreateVertexShader(vertexBlob->GetBufferPointer(), vertexBlob->GetBufferSize(), null, ref vs),
                "ID3D11Device::CreateVertexShader");
            VertexShader = vs;

            ID3D11PixelShader* ps = null;
            D3D11GraphicsDevice.Check(
                device->CreatePixelShader(pixelBlob->GetBufferPointer(), pixelBlob->GetBufferSize(), null, ref ps),
                "ID3D11Device::CreatePixelShader");
            PixelShader = ps;

            VertexShaderBlob = vertexBlob;
            vertexBlob = null;
        }
        finally
        {
            if (pixelBlob is not null) pixelBlob->Release();
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
        if (VertexShader is not null) { VertexShader->Release(); VertexShader = null; }
        if (PixelShader is not null) { PixelShader->Release(); PixelShader = null; }
        if (VertexShaderBlob is not null) { VertexShaderBlob->Release(); VertexShaderBlob = null; }
    }
}
