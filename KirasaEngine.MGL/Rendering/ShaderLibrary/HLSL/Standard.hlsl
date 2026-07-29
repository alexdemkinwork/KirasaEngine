cbuffer FrameConstants : register(b0)
{
    float4x4 ViewProjection;
    float4 LightDirection;
    float4 LightColor;
    float4 AmbientColor;
};

cbuffer DrawConstants : register(b1)
{
    float4 BaseColor;
};

Texture2D BaseColorTexture : register(t0);
SamplerState BaseColorSampler : register(s0);

// Semantic strings here must match each vertex element's Name (upper-cased) byte-for-byte — see the doc
// comment on D3D11Pipeline/D3D12Pipeline's CreateInputLayout for why (they derive InputElementDesc's
// SemanticName directly from VertexElementDescription.Name rather than a hardcoded per-shader table).
struct VSInput
{
    float3 Position : POSITION;
    float3 Normal : NORMAL;
    float4 Color : COLOR;
    float2 UV : TEXCOORD;
    float4 InstanceWorld0 : INSTANCEWORLD0;
    float4 InstanceWorld1 : INSTANCEWORLD1;
    float4 InstanceWorld2 : INSTANCEWORLD2;
    float4 InstanceWorld3 : INSTANCEWORLD3;
    float4 InstanceColor : INSTANCECOLOR;
};

struct PSInput
{
    float4 Position : SV_Position;
    float3 Normal : NORMAL;
    float4 Color : COLOR0;
    float2 UV : TEXCOORD0;
};

PSInput VSMain(VSInput input)
{
    // The four instance attributes are the four ROWS of the System.Numerics matrix, in order.
    // HLSL's float4x4(a, b, c, d) constructor fills ROWS (unlike GLSL's mat4(...), which fills COLUMNS),
    // so `world` here is the numerics matrix laid out exactly as-is: row-vector convention, translation in
    // the last row. That means the vector goes on the LEFT — mul(position, world), not mul(world, position).
    // The column_major storage default is irrelevant for a matrix built from a constructor; it only
    // governs how the cbuffer matrices below are unpacked.
    float4x4 world = float4x4(input.InstanceWorld0, input.InstanceWorld1, input.InstanceWorld2, input.InstanceWorld3);
    float4 worldPos = mul(float4(input.Position, 1.0), world);

    PSInput output;
    // ViewProjection is different: it comes from a cbuffer, where the row-major bytes get unpacked under
    // HLSL's default column_major rule, which already yields the transpose — so the matrix goes on the left.
    output.Position = mul(ViewProjection, worldPos);
    output.Normal = mul(input.Normal, (float3x3)world);
    output.Color = input.Color * input.InstanceColor;
    output.UV = input.UV;
    return output;
}

float4 PSMain(PSInput input) : SV_Target
{
    float3 n = normalize(input.Normal);
    float3 l = normalize(-LightDirection.xyz);
    float ndotl = max(dot(n, l), 0.0);
    float3 lighting = AmbientColor.rgb + LightColor.rgb * LightColor.a * ndotl;

    float4 texColor = BaseColorTexture.Sample(BaseColorSampler, input.UV);
    float4 albedo = BaseColor * input.Color * texColor;

    return float4(lighting * albedo.rgb, albedo.a);
}
