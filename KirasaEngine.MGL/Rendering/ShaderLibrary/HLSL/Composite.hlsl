// Composite HLSL for D3D11/D3D12 (HDR + Bloom -> Tonemap + Color Grade + Vignette)

cbuffer CompositeConstants : register(b0)
{
    float4 Params0; // x = bloomIntensity, y = vignetteIntensity, z = saturation, w = contrast
    float4 Params1; // x = bloomEnabled (0/1), y = vignetteEnabled (0/1), z/w unused
};

Texture2D HdrColorTexture : register(t0);
SamplerState HdrColorSampler : register(s0);
Texture2D BloomTexture : register(t1);
SamplerState BloomSampler : register(s1);

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

float3 AcesTonemap(float3 color)
{
    const float a = 2.51;
    const float b = 0.03;
    const float c = 2.43;
    const float d = 0.59;
    const float e = 0.14;
    return clamp((color * (a * color + b)) / (color * (c * color + d) + e), 0.0, 1.0);
}

float Vignette(float2 uv, float intensity)
{
    float2 center = float2(0.5, 0.5);
    float dist = distance(uv, center) * 1.4142;
    return 1.0 - smoothstep(0.3, 0.9, dist) * intensity;
}

float3 ColorGrade(float3 color, float saturation, float contrast)
{
    float luminance = dot(color, float3(0.2126, 0.7152, 0.0722));
    float3 saturated = lerp(float3(luminance, luminance, luminance), color, saturation);
    return (saturated - 0.5) * contrast + 0.5;
}

float4 PSMain(PSInput input) : SV_Target
{
    float3 hdrColor = HdrColorTexture.Sample(HdrColorSampler, input.UV).rgb;

    if (Params1.x > 0.5)
    {
        float3 bloom = BloomTexture.Sample(BloomSampler, input.UV).rgb;
        hdrColor += bloom * Params0.x;
    }

    float3 tonemapped = AcesTonemap(hdrColor);
    float3 graded = ColorGrade(tonemapped, Params0.z, Params0.w);

    if (Params1.y > 0.5)
    {
        float vig = Vignette(input.UV, Params0.y);
        graded *= vig;
    }

    // sRGB gamma correction
    float3 finalColor = pow(graded, float3(1.0 / 2.2, 1.0 / 2.2, 1.0 / 2.2));

    return float4(finalColor, 1.0);
}