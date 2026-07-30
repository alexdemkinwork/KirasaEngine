// FXAA HLSL for D3D11/D3D12

cbuffer FXAAConstants : register(b0)
{
    float4 Params0; // x = texelSizeX, y = texelSizeY, z/w unused
};

Texture2D SourceTexture : register(t0);
SamplerState SourceSampler : register(s0);

struct VSInput
{
    float2 Position : POSITION;
    float2 UV : TEXCOORD;
};

struct PSInput
{
    float4 Position : SV_Position;
    float2 UV : TEXCOORD;
};

PSInput VSMain(VSInput input)
{
    PSInput output;
    output.Position = float4(input.Position, 0.0, 1.0);
    output.UV = input.UV;
    return output;
}

#define FXAA_REDUCE_MIN   (1.0/128.0)
#define FXAA_REDUCE_MUL   (1.0/8.0)
#define FXAA_SPAN_MAX     8.0

float4 PSMain(PSInput input) : SV_Target
{
    float2 texelSize = float2(Params0.x, Params0.y);
    float2 uv = input.UV;

    float3 rgbNW = SourceTexture.Sample(SourceSampler, uv + float2(-texelSize.x, -texelSize.y)).rgb;
    float3 rgbNE = SourceTexture.Sample(SourceSampler, uv + float2( texelSize.x, -texelSize.y)).rgb;
    float3 rgbSW = SourceTexture.Sample(SourceSampler, uv + float2(-texelSize.x,  texelSize.y)).rgb;
    float3 rgbSE = SourceTexture.Sample(SourceSampler, uv + float2( texelSize.x,  texelSize.y)).rgb;
    float3 rgbM  = SourceTexture.Sample(SourceSampler, uv).rgb;

    float lumaNW = dot(rgbNW, float3(0.299, 0.587, 0.114));
    float lumaNE = dot(rgbNE, float3(0.299, 0.587, 0.114));
    float lumaSW = dot(rgbSW, float3(0.299, 0.587, 0.114));
    float lumaSE = dot(rgbSE, float3(0.299, 0.587, 0.114));
    float lumaM  = dot(rgbM,  float3(0.299, 0.587, 0.114));

    float lumaMin = min(lumaM, min(min(lumaNW, lumaNE), min(lumaSW, lumaSE)));
    float lumaMax = max(lumaM, max(max(lumaNW, lumaNE), max(lumaSW, lumaSE)));

    float2 dir;
    dir.x = -((lumaNW + lumaNE) - (lumaSW + lumaSE));
    dir.y =  ((lumaNW + lumaSW) - (lumaNE + lumaSE));

    float dirReduce = max((lumaNW + lumaNE + lumaSW + lumaSE) * (0.25 * FXAA_REDUCE_MUL), FXAA_REDUCE_MIN);

    float rcpDirMin = 1.0 / (min(abs(dir.x), abs(dir.y)) + dirReduce);
    dir = min(float2(FXAA_SPAN_MAX, FXAA_SPAN_MAX),
              max(float2(-FXAA_SPAN_MAX, -FXAA_SPAN_MAX),
              dir * rcpDirMin)) * texelSize;

    float3 rgbA = 0.5 * (
        SourceTexture.Sample(SourceSampler, uv + dir * (1.0/3.0 - 0.5)).rgb +
        SourceTexture.Sample(SourceSampler, uv + dir * (2.0/3.0 - 0.5)).rgb);
    float3 rgbB = rgbA * 0.5 + 0.25 * (
        SourceTexture.Sample(SourceSampler, uv + dir * -0.5).rgb +
        SourceTexture.Sample(SourceSampler, uv + dir *  0.5).rgb);

    float lumaB = dot(rgbB, float3(0.299, 0.587, 0.114));
    
    if ((lumaB < lumaMin) || (lumaB > lumaMax))
        return float4(rgbA, 1.0);
    else
        return float4(rgbB, 1.0);
}