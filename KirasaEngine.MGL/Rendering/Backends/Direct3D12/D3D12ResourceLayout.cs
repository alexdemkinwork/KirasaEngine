using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;

using KirasaEngine.MGL.Rendering;

namespace KirasaEngine.MGL.Rendering.Backends.Direct3D12;

/// <summary>
/// The D3D12 counterpart of a resource layout is a real GPU object: a root signature. Where the OpenGL and
/// D3D11 backends can treat <see cref="ResourceLayoutDescription"/> as pure metadata and bind each resource
/// individually at draw time, D3D12 requires the whole binding shape to be declared up front.
///
/// <para><b>Mapping</b> — one root parameter per layout element, in element order:</para>
/// <list type="bullet">
///   <item><description><see cref="ResourceKind.UniformBuffer"/> → a <b>root CBV</b>
///   (<c>ROOT_PARAMETER_TYPE_CBV</c>). Root descriptors cost no descriptor-heap traffic and are the cheapest
///   way to feed small, frequently rewritten constants — exactly FrameConstants/DrawConstants here.</description></item>
///   <item><description><see cref="ResourceKind.TextureReadOnly"/> → a <b>descriptor table</b> holding one
///   SRV range. SRVs cannot be root descriptors for textures, so a table is mandatory.</description></item>
///   <item><description><see cref="ResourceKind.Sampler"/> → a <b>descriptor table</b> holding one SAMPLER
///   range, pointing into the device's shader-visible sampler heap (see <see cref="D3D12Sampler"/> for why a
///   static sampler wasn't used instead).</description></item>
/// </list>
///
/// <para><b>Register numbers</b> come straight from each element's <c>Binding</c>, exactly as in the OpenGL
/// and D3D11 backends — HLSL's b/t/s register spaces are independent, so the reuse of 0 across kinds in
/// <see cref="ShaderResourceLayouts"/> is fine here (Vulkan is the sole backend that must remap; see that
/// class's doc comment).</para>
/// </summary>
internal sealed unsafe class D3D12ResourceLayout : IResourceLayout
{
    public ResourceLayoutDescription Description { get; }

    public ID3D12RootSignature* RootSignature;

    /// <summary>Root parameter index for each layout element, positionally aligned with <c>Description.Elements</c>.</summary>
    public uint[] RootParameterIndices { get; }

    public D3D12ResourceLayout(D3D12GraphicsDevice device, ResourceLayoutDescription description)
    {
        Description = description;

        var elements = description.Elements;
        RootParameterIndices = new uint[elements.Length];

        var parameters = new RootParameter[elements.Length];
        var ranges = new DescriptorRange[elements.Length];

        fixed (DescriptorRange* rangeBase = ranges)
        {
            for (var i = 0; i < elements.Length; i++)
            {
                var element = elements[i];
                RootParameterIndices[i] = (uint)i;

                var visibility = D3D12Formats.MapVisibility(element.Stages);

                switch (element.Kind)
                {
                    case ResourceKind.UniformBuffer:
                        parameters[i] = new RootParameter
                        {
                            ParameterType = RootParameterType.TypeCbv,
                            ShaderVisibility = visibility,
                        };
                        parameters[i].Anonymous.Descriptor = new RootDescriptor
                        {
                            ShaderRegister = element.Binding,
                            RegisterSpace = 0,
                        };
                        break;

                    case ResourceKind.TextureReadOnly:
                    case ResourceKind.Sampler:
                        rangeBase[i] = new DescriptorRange
                        {
                            RangeType = element.Kind == ResourceKind.Sampler
                                ? DescriptorRangeType.Sampler
                                : DescriptorRangeType.Srv,
                            NumDescriptors = 1,
                            BaseShaderRegister = element.Binding,
                            RegisterSpace = 0,
                            OffsetInDescriptorsFromTableStart = 0,
                        };
                        parameters[i] = new RootParameter
                        {
                            ParameterType = RootParameterType.TypeDescriptorTable,
                            ShaderVisibility = visibility,
                        };
                        parameters[i].Anonymous.DescriptorTable = new RootDescriptorTable
                        {
                            NumDescriptorRanges = 1,
                            PDescriptorRanges = rangeBase + i,
                        };
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(description), element.Kind, "Unsupported resource kind.");
                }
            }

            fixed (RootParameter* parameterBase = parameters)
            {
                var rootSignatureDesc = new RootSignatureDesc
                {
                    NumParameters = (uint)parameters.Length,
                    PParameters = parameterBase,
                    NumStaticSamplers = 0,
                    PStaticSamplers = null,
                    Flags = RootSignatureFlags.AllowInputAssemblerInputLayout,
                };

                ID3D10Blob* serialized = null;
                ID3D10Blob* errors = null;
                var hr = D3D12.GetApi().SerializeRootSignature(
                    &rootSignatureDesc,
                    D3DRootSignatureVersion.Version1,
                    &serialized,
                    &errors);

                if (hr < 0 || serialized is null)
                {
                    var message = errors is not null
                        ? SilkMarshal.PtrToString((nint)errors->GetBufferPointer(), NativeStringEncoding.UTF8)
                        : null;
                    if (errors is not null) errors->Release();
                    if (serialized is not null) serialized->Release();
                    throw new InvalidOperationException($"D3D12SerializeRootSignature failed (HRESULT 0x{hr:X8}): {message}");
                }

                if (errors is not null) errors->Release();

                try
                {
                    ID3D12RootSignature* rootSignature = null;
                    D3D12Util.Check(
                        device.NativeDevice->CreateRootSignature(
                            0,
                            serialized->GetBufferPointer(),
                            serialized->GetBufferSize(),
                            SilkMarshal.GuidPtrOf<ID3D12RootSignature>(),
                            (void**)&rootSignature),
                        "ID3D12Device::CreateRootSignature");
                    RootSignature = rootSignature;
                }
                finally
                {
                    serialized->Release();
                }
            }
        }
    }

    public void Dispose()
    {
        if (RootSignature is null) return;
        RootSignature->Release();
        RootSignature = null;
    }
}
