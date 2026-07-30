// DepthNormalPrepass HLSL for D3D11/D3D12

cbuffer PrepassConstants : register(b0)
{
    float4x4 ViewProjection;
    float4x4 View;
};

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
    float3 Normal : NORMAL0;
    float Depth : TEXCOORD0;
};

PSInput VSMain(VSInput input)
{
    float4x4 world = float4x4(input.InstanceWorld0, input.InstanceWorld1, input.InstanceWorld2, input.InstanceWorld3);
    float4 worldPos = mul(float4(input.Position, 1.0), world);
    float4 viewPos = mul(View, worldPos);

    PSInput output;
    output.Position = mul(ViewProjection, worldPos);
    output.Normal = mul(input.Normal, (float3x3)View * (float3x3)world);
    output.Depth = -viewPos.z;
    return output;
}

float4 PSMain(PSInput input) : SV_Target
{
    return float4(normalize(input.Normal), input.Depth);
}