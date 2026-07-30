using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;
using Silk.NET.DXGI;

using KirasaEngine.MGL.Rendering;

namespace KirasaEngine.MGL.Rendering.Backends.Direct3D12;

/// <summary>
/// Collapses the whole of <see cref="PipelineDescription"/> — shaders, input layout, rasterizer,
/// depth-stencil, blend, render-target formats and the root signature — into one immutable
/// ID3D12PipelineState. D3D12's monolithic PSO is why <see cref="D3D12CommandList.SetPipeline"/> is three
/// calls rather than D3D11's five *SetState calls.
///
/// <para>No PSO cache is kept here: SceneRenderer already caches pipelines per (shader, blend, double-sided),
/// so a second layer would never hit.</para>
/// </summary>
internal sealed unsafe class D3D12Pipeline : IPipeline
{
    public PipelineDescription Description { get; }
    public D3D12ShaderSet ShaderSet { get; }
    public D3D12ResourceLayout ResourceLayout { get; }
    public D3DPrimitiveTopology Topology { get; }

    public ID3D12PipelineState* PipelineState;
    public ID3D12RootSignature* RootSignature => ResourceLayout.RootSignature;

    public D3D12Pipeline(D3D12GraphicsDevice device, PipelineDescription description)
    {
        Description = description;
        ShaderSet = (D3D12ShaderSet)description.ShaderSet;
        ResourceLayout = (D3D12ResourceLayout)description.ResourceLayout;
        Topology = D3D12Formats.MapTopology(description.Topology);

        var layouts = ShaderSet.VertexLayouts;
        var total = 0;
        foreach (var layout in layouts) total += layout.Elements.Length;

        var inputElements = new InputElementDesc[total];
        var namePointers = new nint[total];

        try
        {
            // D3D matches vertex inputs by semantic name + semantic index rather than by an integer location,
            // so the abstraction's VertexElementDescription.Location is meaningless here. Instead each
            // element's own Name, upper-cased, is used as the HLSL semantic — HLSL does not require semantic
            // names to be reserved words, it only needs the string used here to match the one declared on
            // the shader's VSInput struct. Crucially, HLSL itself splits a *trailing run of digits* off any
            // semantic into a separate index (so `: INSTANCEWORLD0` compiles to base name "INSTANCEWORLD" +
            // index 0, not the literal string "INSTANCEWORLD0" + index 0) — SplitTrailingDigits mirrors that
            // rule so "InstanceWorld0".."InstanceWorld3" resolve to base "INSTANCEWORLD" with indices 0-3,
            // matching what the compiled shader's input signature actually expects. This replaced a
            // hardcoded per-slot semantic table that assumed every pipeline used exactly Standard.hlsl's
            // two-slot (VertexPNCT, InstanceData) shape.
            var index = 0;
            for (var slot = 0; slot < layouts.Length; slot++)
            {
                var layout = layouts[slot];
                var perInstance = layout.InputRate == VertexInputRate.PerInstance;

                for (var e = 0; e < layout.Elements.Length; e++)
                {
                    var element = layout.Elements[e];
                    var (semanticName, semanticIndex) = SplitTrailingDigits(element.Name.ToUpperInvariant());
                    namePointers[index] = Marshal.StringToHGlobalAnsi(semanticName);

                    inputElements[index] = new InputElementDesc
                    {
                        SemanticName = (byte*)namePointers[index],
                        SemanticIndex = semanticIndex,
                        Format = D3D12Formats.MapVertexElement(element.Format),
                        InputSlot = (uint)slot,
                        AlignedByteOffset = element.Offset,
                        InputSlotClass = perInstance ? InputClassification.PerInstanceData : InputClassification.PerVertexData,
                        InstanceDataStepRate = perInstance ? 1u : 0u,
                    };
                    index++;
                }
            }

            fixed (InputElementDesc* inputElementPtr = inputElements)
            {
                var desc = new GraphicsPipelineStateDesc
                {
                    PRootSignature = ResourceLayout.RootSignature,
                    VS = new ShaderBytecode
                    {
                        PShaderBytecode = ShaderSet.VertexShaderBlob->GetBufferPointer(),
                        BytecodeLength = ShaderSet.VertexShaderBlob->GetBufferSize(),
                    },
                    PS = new ShaderBytecode
                    {
                        PShaderBytecode = ShaderSet.PixelShaderBlob->GetBufferPointer(),
                        BytecodeLength = ShaderSet.PixelShaderBlob->GetBufferSize(),
                    },
                    BlendState = CreateBlendState(description),
                    SampleMask = uint.MaxValue,
                    RasterizerState = CreateRasterizerState(description),
                    DepthStencilState = CreateDepthStencilState(description),
                    InputLayout = new InputLayoutDesc
                    {
                        PInputElementDescs = inputElementPtr,
                        NumElements = (uint)total,
                    },
                    IBStripCutValue = IndexBufferStripCutValue.ValueDisabled,
                    PrimitiveTopologyType = D3D12Formats.MapTopologyType(description.Topology),
                    NumRenderTargets = 1,
                    DSVFormat = description.DepthFormat is { } depthFormat
                        ? D3D12Formats.MapDsv(depthFormat)
                        : Format.FormatUnknown,
                    SampleDesc = new SampleDesc(1, 0),
                    NodeMask = 0,
                    Flags = PipelineStateFlags.None,
                };
                desc.RTVFormats[0] = D3D12Formats.MapRtv(description.ColorFormat);

                ID3D12PipelineState* pipelineState = null;
                D3D12Util.Check(
                    device.NativeDevice->CreateGraphicsPipelineState(&desc, SilkMarshal.GuidPtrOf<ID3D12PipelineState>(), (void**)&pipelineState),
                    "ID3D12Device::CreateGraphicsPipelineState");
                PipelineState = pipelineState;
            }
        }
        finally
        {
            foreach (var ptr in namePointers)
                if (ptr != 0) Marshal.FreeHGlobal(ptr);
        }
    }

    /// <summary>Mirrors HLSL's own semantic-parsing rule: a trailing run of ASCII digits is the index, the rest is the base name.</summary>
    private static (string Name, uint Index) SplitTrailingDigits(string name)
    {
        var i = name.Length;
        while (i > 0 && char.IsAsciiDigit(name[i - 1])) i--;
        return i == name.Length ? (name, 0u) : (name[..i], uint.Parse(name[i..]));
    }

    private static RasterizerDesc CreateRasterizerState(PipelineDescription description) => new()
    {
        FillMode = D3D12Formats.MapFillMode(description.FillMode),
        CullMode = D3D12Formats.MapCullMode(description.CullMode),
        FrontCounterClockwise = description.FrontFace == Abstractions.Enums.FrontFace.CounterClockwise,
        DepthBias = 0,
        DepthBiasClamp = 0f,
        SlopeScaledDepthBias = 0f,
        DepthClipEnable = true,
        MultisampleEnable = false,
        AntialiasedLineEnable = false,
        ForcedSampleCount = 0,
        ConservativeRaster = ConservativeRasterizationMode.Off,
    };

    private static DepthStencilDesc CreateDepthStencilState(PipelineDescription description)
    {
        var stencilOp = new DepthStencilopDesc
        {
            StencilFailOp = StencilOp.Keep,
            StencilDepthFailOp = StencilOp.Keep,
            StencilPassOp = StencilOp.Keep,
            StencilFunc = ComparisonFunc.Always,
        };

        // With no depth attachment there is nothing to test against; leaving DepthEnable on would make the
        // PSO invalid against a DSVFormat of UNKNOWN.
        var depthEnabled = description.DepthFormat is not null && description.DepthTestEnabled;

        return new DepthStencilDesc
        {
            DepthEnable = depthEnabled,
            DepthWriteMask = description.DepthFormat is not null && description.DepthWriteEnabled
                ? DepthWriteMask.All
                : DepthWriteMask.Zero,
            DepthFunc = D3D12Formats.MapCompare(description.DepthCompare),
            StencilEnable = false,
            StencilReadMask = 0xFF,
            StencilWriteMask = 0xFF,
            FrontFace = stencilOp,
            BackFace = stencilOp,
        };
    }

    private static BlendDesc CreateBlendState(PipelineDescription description)
    {
        var target = description.Blend switch
        {
            BlendMode.Opaque => new RenderTargetBlendDesc
            {
                BlendEnable = false,
                LogicOpEnable = false,
                SrcBlend = Blend.One,
                DestBlend = Blend.Zero,
                BlendOp = BlendOp.Add,
                SrcBlendAlpha = Blend.One,
                DestBlendAlpha = Blend.Zero,
                BlendOpAlpha = BlendOp.Add,
                LogicOp = LogicOp.Noop,
                RenderTargetWriteMask = (byte)ColorWriteEnable.All,
            },
            BlendMode.AlphaBlend => new RenderTargetBlendDesc
            {
                BlendEnable = true,
                LogicOpEnable = false,
                SrcBlend = Blend.SrcAlpha,
                DestBlend = Blend.InvSrcAlpha,
                BlendOp = BlendOp.Add,
                SrcBlendAlpha = Blend.One,
                DestBlendAlpha = Blend.InvSrcAlpha,
                BlendOpAlpha = BlendOp.Add,
                LogicOp = LogicOp.Noop,
                RenderTargetWriteMask = (byte)ColorWriteEnable.All,
            },
            BlendMode.Additive => new RenderTargetBlendDesc
            {
                BlendEnable = true,
                LogicOpEnable = false,
                SrcBlend = Blend.SrcAlpha,
                DestBlend = Blend.One,
                BlendOp = BlendOp.Add,
                SrcBlendAlpha = Blend.One,
                DestBlendAlpha = Blend.One,
                BlendOpAlpha = BlendOp.Add,
                LogicOp = LogicOp.Noop,
                RenderTargetWriteMask = (byte)ColorWriteEnable.All,
            },
            _ => throw new ArgumentOutOfRangeException(nameof(description)),
        };

        var blend = new BlendDesc { AlphaToCoverageEnable = false, IndependentBlendEnable = false };
        blend.RenderTarget[0] = target;
        return blend;
    }

    public void Dispose()
    {
        if (PipelineState is not null) { PipelineState->Release(); PipelineState = null; }

        // The IShaderSet and IResourceLayout are owned by whoever created them (SceneRenderer creates one
        // shader set per pipeline and never disposes it separately), so mirror OpenGL/D3D11 and leave them.
    }
}
