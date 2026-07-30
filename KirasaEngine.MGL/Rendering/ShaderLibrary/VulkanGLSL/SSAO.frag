#version 460 core

layout(location = 0) in vec2 vUV;

layout(set = 0, binding = 0) uniform SSAOConstants
{
    vec4 Params0; // x = tanHalfFovY, y = aspect, z = radius, w = power
    vec4 Params1; // x = sampleCount, y = bias, z/w unused
};

layout(set = 0, binding = 1) uniform texture2D NormalDepthTexture;
layout(set = 0, binding = 2) uniform sampler NormalDepthSampler;

layout(location = 0) out float oOcclusion;

vec3 HemisphereSample(int i, int count, float seed)
{
    float phi = (float(i) + seed) * 2.399963;
    float r = sqrt((float(i) + 0.5) / float(count));
    float x = r * cos(phi);
    float y = r * sin(phi);
    float z = sqrt(max(0.0, 1.0 - x * x - y * y));
    return vec3(x, y, z);
}

float Hash(vec2 p)
{
    return fract(sin(dot(p, vec2(12.9898, 78.233))) * 43758.5453);
}

void main()
{
    vec4 normalDepth = texture(sampler2D(NormalDepthTexture, NormalDepthSampler), vUV);
    vec3 normal = normalize(normalDepth.xyz);
    float depth = normalDepth.w;

    if (depth <= 0.0)
    {
        oOcclusion = 1.0;
        return;
    }

    float tanHalfFovY = Params0.x;
    float aspect = Params0.y;
    float radius = Params0.z;
    float power = Params0.w;
    int sampleCount = int(Params1.x);
    float bias = Params1.y;

    vec2 ndc = vUV * 2.0 - 1.0;
    vec3 viewRayDir = vec3(ndc.x * tanHalfFovY * aspect, ndc.y * tanHalfFovY, -1.0);
    vec3 viewPos = viewRayDir * depth;

    vec3 randomVec = normalize(vec3(Hash(vUV) * 2.0 - 1.0, Hash(vUV.yx) * 2.0 - 1.0, 0.0) + vec3(0.0001));
    vec3 tangent = normalize(randomVec - normal * dot(randomVec, normal));
    vec3 bitangent = cross(normal, tangent);
    mat3 tbn = mat3(tangent, bitangent, normal);

    float seed = Hash(vUV) * 6.2831853;
    float occlusion = 0.0;

    for (int i = 0; i < sampleCount; i++)
    {
        vec3 samplePos = viewPos + (tbn * HemisphereSample(i, sampleCount, seed)) * radius;

        float sampleNegZ = max(-samplePos.z, 0.0001);
        vec2 sampleUV = vec2(samplePos.x / sampleNegZ / (tanHalfFovY * aspect),
                             samplePos.y / sampleNegZ / tanHalfFovY) * 0.5 + 0.5;

        if (sampleUV.x < 0.0 || sampleUV.x > 1.0 || sampleUV.y < 0.0 || sampleUV.y > 1.0)
            continue;

        float sampleStoredDepth = texture(sampler2D(NormalDepthTexture, NormalDepthSampler), sampleUV).w;
        float sampleViewDepth = -samplePos.z;

        float rangeCheck = smoothstep(0.0, 1.0, radius / max(abs(depth - sampleStoredDepth), 0.0001));
        occlusion += (sampleStoredDepth < sampleViewDepth - bias ? 1.0 : 0.0) * rangeCheck;
    }

    occlusion = 1.0 - occlusion / max(float(sampleCount), 1.0);
    oOcclusion = pow(clamp(occlusion, 0.0, 1.0), power);
}