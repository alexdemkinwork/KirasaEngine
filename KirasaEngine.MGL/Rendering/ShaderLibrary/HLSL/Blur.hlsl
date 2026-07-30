// Blur HLSL for D3D11/D3D12 (used for bloom: bright-pass + horizontal blur, then vertical blur)

cbuffer BlurConstants : register(b0)
{
    float4 Params0; // x = texelSizeX, y = texelSizeY, z = horizontal (0/1), w = blurRadius
    float4 Params1; // x = threshold, y = applyThreshold (0/1), z/w unused
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

float4 PSMain(PSInput input) : SV_Target
{
    float horizontal = Params0.z;
    float radius = Params0.w;
    float threshold = Params1.x;
    bool applyThreshold = Params1.y > 0.5;

    float2 texelSize = float2(Params0.x, Params0.y);
    float4 color = float4(0, 0, 0, 0);
    float weightSum = 0.0;

    for (int i = -4; i <= 4; i++)
    {
        float offset = float(i);
        float weight = exp(-0.5 * (offset * offset) / (radius * radius));
        
        float2 sampleUV = input.UV;
        if (horizontal > 0.5)
            sampleUV.x += offset * texelSize.x;
        else
            sampleUV.y += offset * texelSize.y;

        float4 sampleColor = SourceTexture.Sample(SourceSampler, sampleUV);
        
        if (applyThreshold)
        {
            float luminance = dot(sampleColor.rgb, float3(0.2126, 0.7152, 0.0722));
            if (luminance < threshold)
                sampleColor = float4(0, 0, 0, 0);
        }

        color += sampleColor * weight;
        weightSum += weight;
    }

    return color / weightSum;
}