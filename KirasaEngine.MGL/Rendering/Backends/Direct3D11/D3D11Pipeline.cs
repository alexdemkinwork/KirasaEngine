using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;

using KirasaEngine.MGL.Rendering;

namespace KirasaEngine.MGL.Rendering.Backends.Direct3D11;

/// <summary>
/// Bakes the pieces of <see cref="PipelineDescription"/> that D3D11 exposes as immutable state objects
/// (input layout, rasterizer, depth-stencil, blend) so <see cref="D3D11CommandList.SetPipeline"/> is a
/// handful of *SetState calls.
/// </summary>
internal sealed unsafe class D3D11Pipeline : IPipeline
{
    public PipelineDescription Description { get; }
    public D3D11ShaderSet ShaderSet { get; }
    public D3DPrimitiveTopology Topology { get; }

    public ID3D11InputLayout* InputLayout;
    public ID3D11RasterizerState* RasterizerState;
    public ID3D11RasterizerState* RasterizerStateScissor;
    public ID3D11DepthStencilState* DepthStencilState;
    public ID3D11BlendState* BlendState;

    public D3D11Pipeline(ID3D11Device* device, PipelineDescription description)
    {
        Description = description;
        ShaderSet = (D3D11ShaderSet)description.ShaderSet;
        Topology = D3D11Formats.MapTopology(description.Topology);

        CreateInputLayout(device);
        RasterizerState = CreateRasterizerState(device, description, scissorEnable: false);
        RasterizerStateScissor = CreateRasterizerState(device, description, scissorEnable: true);
        CreateDepthStencilState(device, description);
        CreateBlendState(device, description);
    }

    /// <summary>
    /// D3D matches vertex inputs by semantic name + semantic index, not by an integer location, so the
    /// abstraction's <see cref="VertexElementDescription.Location"/> is meaningless here. Instead each
    /// element's own <see cref="VertexElementDescription.Name"/>, upper-cased, is used as the HLSL semantic
    /// — HLSL does not require semantic names to be reserved words, it only needs the string used in
    /// <c>CreateInputLayout</c> to match the one declared on the shader's VSInput struct. Crucially, HLSL
    /// itself splits a *trailing run of digits* off any semantic into a separate index (so
    /// <c>: INSTANCEWORLD0</c> compiles to base name "INSTANCEWORLD" + index 0, not the literal string
    /// "INSTANCEWORLD0" + index 0) — <see cref="SplitTrailingDigits"/> mirrors that rule so
    /// "InstanceWorld0".."InstanceWorld3" resolve to base "INSTANCEWORLD" with indices 0-3, matching what the
    /// compiled shader's input signature actually expects. This replaced an earlier hardcoded per-slot
    /// semantic table that assumed every pipeline used exactly Standard.hlsl's two-slot (VertexPNCT,
    /// InstanceData) shape and could not describe any other vertex layout (post-process/shadow/prepass
    /// shaders need their own, smaller ones).
    /// </summary>
    private void CreateInputLayout(ID3D11Device* device)
    {
        var layouts = ShaderSet.VertexLayouts;
        var total = 0;
        foreach (var layout in layouts) total += layout.Elements.Length;

        var descs = new InputElementDesc[total];
        var namePointers = new nint[total];

        try
        {
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

                    descs[index] = new InputElementDesc
                    {
                        SemanticName = (byte*)namePointers[index],
                        SemanticIndex = semanticIndex,
                        Format = D3D11Formats.MapVertexElement(element.Format),
                        InputSlot = (uint)slot,
                        AlignedByteOffset = element.Offset,
                        InputSlotClass = perInstance ? InputClassification.PerInstanceData : InputClassification.PerVertexData,
                        InstanceDataStepRate = perInstance ? 1u : 0u,
                    };
                    index++;
                }
            }

            ID3D11InputLayout* inputLayout = null;
            fixed (InputElementDesc* descPtr = descs)
            {
                D3D11GraphicsDevice.Check(
                    device->CreateInputLayout(
                        descPtr,
                        (uint)total,
                        ShaderSet.VertexShaderBlob->GetBufferPointer(),
                        ShaderSet.VertexShaderBlob->GetBufferSize(),
                        ref inputLayout),
                    "ID3D11Device::CreateInputLayout");
            }
            InputLayout = inputLayout;
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

    private static ID3D11RasterizerState* CreateRasterizerState(ID3D11Device* device, PipelineDescription description, bool scissorEnable)
    {
        var desc = new RasterizerDesc
        {
            FillMode = D3D11Formats.MapFillMode(description.FillMode),
            CullMode = D3D11Formats.MapCullMode(description.CullMode),
            FrontCounterClockwise = description.FrontFace == Abstractions.Enums.FrontFace.CounterClockwise,
            DepthBias = 0,
            DepthBiasClamp = 0f,
            SlopeScaledDepthBias = 0f,
            DepthClipEnable = true,
            ScissorEnable = scissorEnable,
            MultisampleEnable = false,
            AntialiasedLineEnable = false,
        };

        ID3D11RasterizerState* state = null;
        D3D11GraphicsDevice.Check(device->CreateRasterizerState(&desc, ref state), "ID3D11Device::CreateRasterizerState");
        return state;
    }

    private void CreateDepthStencilState(ID3D11Device* device, PipelineDescription description)
    {
        var stencilOp = new DepthStencilopDesc
        {
            StencilFailOp = StencilOp.Keep,
            StencilDepthFailOp = StencilOp.Keep,
            StencilPassOp = StencilOp.Keep,
            StencilFunc = ComparisonFunc.Always,
        };

        var desc = new DepthStencilDesc
        {
            DepthEnable = description.DepthTestEnabled,
            DepthWriteMask = description.DepthWriteEnabled ? DepthWriteMask.All : DepthWriteMask.Zero,
            DepthFunc = D3D11Formats.MapCompare(description.DepthCompare),
            StencilEnable = false,
            StencilReadMask = 0xFF,
            StencilWriteMask = 0xFF,
            FrontFace = stencilOp,
            BackFace = stencilOp,
        };

        ID3D11DepthStencilState* state = null;
        D3D11GraphicsDevice.Check(device->CreateDepthStencilState(&desc, ref state), "ID3D11Device::CreateDepthStencilState");
        DepthStencilState = state;
    }

    private void CreateBlendState(ID3D11Device* device, PipelineDescription description)
    {
        var target = description.Blend switch
        {
            BlendMode.Opaque => new RenderTargetBlendDesc
            {
                BlendEnable = false,
                SrcBlend = Blend.One,
                DestBlend = Blend.Zero,
                BlendOp = BlendOp.Add,
                SrcBlendAlpha = Blend.One,
                DestBlendAlpha = Blend.Zero,
                BlendOpAlpha = BlendOp.Add,
                RenderTargetWriteMask = (byte)ColorWriteEnable.All,
            },
            BlendMode.AlphaBlend => new RenderTargetBlendDesc
            {
                BlendEnable = true,
                SrcBlend = Blend.SrcAlpha,
                DestBlend = Blend.InvSrcAlpha,
                BlendOp = BlendOp.Add,
                SrcBlendAlpha = Blend.One,
                DestBlendAlpha = Blend.InvSrcAlpha,
                BlendOpAlpha = BlendOp.Add,
                RenderTargetWriteMask = (byte)ColorWriteEnable.All,
            },
            BlendMode.Additive => new RenderTargetBlendDesc
            {
                BlendEnable = true,
                SrcBlend = Blend.SrcAlpha,
                DestBlend = Blend.One,
                BlendOp = BlendOp.Add,
                SrcBlendAlpha = Blend.One,
                DestBlendAlpha = Blend.One,
                BlendOpAlpha = BlendOp.Add,
                RenderTargetWriteMask = (byte)ColorWriteEnable.All,
            },
            _ => throw new ArgumentOutOfRangeException(nameof(description)),
        };

        var desc = new BlendDesc { AlphaToCoverageEnable = false, IndependentBlendEnable = false };
        desc.RenderTarget[0] = target;

        ID3D11BlendState* state = null;
        D3D11GraphicsDevice.Check(device->CreateBlendState(&desc, ref state), "ID3D11Device::CreateBlendState");
        BlendState = state;
    }

    public void Dispose()
    {
        if (InputLayout is not null) { InputLayout->Release(); InputLayout = null; }
        if (RasterizerState is not null) { RasterizerState->Release(); RasterizerState = null; }
        if (RasterizerStateScissor is not null) { RasterizerStateScissor->Release(); RasterizerStateScissor = null; }
        if (DepthStencilState is not null) { DepthStencilState->Release(); DepthStencilState = null; }
        if (BlendState is not null) { BlendState->Release(); BlendState = null; }

        // The IShaderSet is owned by whoever created it (SceneRenderer creates one per pipeline and never
        // disposes it separately), so mirror the OpenGL backend and leave it alone here.
    }
}
