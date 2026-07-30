// SSAO HLSL for D3D11/D3D12

cbuffer SSAOConstants : register(b0)
{
    float4 Params0; // x = tanHalfFovY, y = aspect, z = radius, w = power
    float4 Params1; // x = sampleCount, y = bias, z/w unused
};

Texture2D NormalDepthTexture : register(t0);
SamplerState NormalDepthSampler : register(s0);

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

float3 HemisphereSample(int i, int count, float seed)
{
    float phi = (float(i) + seed) * 2.399963;
    float r = sqrt((float(i) + 0.5) / float(count));
    float x = r * cos(phi);
    float y = r * sin(phi);
    float z = sqrt(max(0.0, 1.0 - x * x - y * y));
    return float3(x, y, z);
}

float Hash(float2 p)
{
    return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
}

float PSMain(PSInput input) : SV_Target
{
    float4 normalDepth = NormalDepthTexture.Sample(NormalDepthSampler, input.UV);
    float3 normal = normalize(normalDepth.xyz);
    float depth = normalDepth.w;

    if (depth <= 0.0)
        return 1.0;

    float tanHalfFovY = Params0.x;
    float aspect = Params0.y;
    float radius = Params0.z;
    float power = Params0.w;
    int sampleCount = int(Params1.x);
    float bias = Params1.y;

    float2 ndc = input.UV * 2.0 - 1.0;
    float3 viewRayDir = float3(ndc.x * tanHalfFovY * aspect, ndc.y * tanHalfFovY, -1.0);
    float3 viewPos = viewRayDir * depth;

    float3 randomVec = normalize(float3(Hash(input.UV) * 2.0 - 1.0, Hash(input.UV.yx) * 2.0 - 1.0, 0.0) + 0.0001);
    float3 tangent = normalize(randomVec - normal * dot(randomVec, normal));
    float3 bitangent = cross(normal, tangent);
    float3x3 tbn = float3x3(tangent, bitangent, normal);

    float seed = Hash(input.UV) * 6.2831853;
    float occlusion = 0.0;

    [unroll(64)]
    for (int i = 0; i < sampleCount; i++)
    {
        float3 samplePos = viewPos + mul(tbn, HemisphereSample(i, sampleCount, seed)) * radius;

        float sampleNegZ = max(-samplePos.z, 0.0001);
        float2 sampleUV = float2(samplePos.x / sampleNegZ / (tanHalfFovY * aspect),
                                 samplePos.y / sampleNegZ / tanHalfFovY) * 0.5 + 0.5;

        if (sampleUV.x < 0.0 || sampleUV.x > 1.0 || sampleUV.y < 0.0 || sampleUV.y > 1.0)
            continue;

        float sampleStoredDepth = NormalDepthTexture.Sample(NormalDepthSampler, sampleUV).w;
        float sampleViewDepth = -samplePos.z;

        float rangeCheck = smoothstep(0.0, 1.0, radius / max(abs(depth - sampleStoredDepth), 0.0001));
        occlusion += (sampleStoredDepth < sampleViewDepth - bias ? 1.0 : 0.0) * rangeCheck;
    }

    occlusion = 1.0 - occlusion / max(float(sampleCount), 1.0);
    return pow(clamp(occlusion, 0.0, 1.0), power);
}