#version 460 core

layout(location = 0) in vec3 vNormal;
layout(location = 1) in vec4 vColor;
layout(location = 2) in vec2 vUV;
layout(location = 3) in vec4 vWorldPos;

layout(std140, binding = 0) uniform FrameConstants
{
    mat4 ViewProjection;
    mat4 LightViewProjection;
    vec4 LightDirection;
    vec4 LightColor;
    vec4 AmbientColor;
    vec4 ShadowParams;
    vec4 ScreenParams;
    vec4 CameraPosition;
};

layout(std140, binding = 1) uniform DrawConstants
{
    vec4 BaseColor;
    vec4 SpecularParams;
};

layout(binding = 0) uniform sampler2D BaseColorTexture;
layout(binding = 1) uniform sampler2D ShadowMapTexture;
layout(binding = 2) uniform sampler2D AOTexture;

layout(location = 0) out vec4 oColor;

float SampleShadow(vec4 worldPos)
{
    if (ShadowParams.w == 0.0) return 1.0;
    vec4 shadowCoord = LightViewProjection * worldPos;
    shadowCoord.xyz /= shadowCoord.w;
    shadowCoord.xy = shadowCoord.xy * 0.5 + 0.5;
    if (shadowCoord.z >= 1.0) return 1.0;
    float bias = ShadowParams.y;
    int pcfRadius = int(ShadowParams.z);
    if (pcfRadius > 0)
    {
        float shadowSum = 0.0;
        int taps = pcfRadius * 2 + 1;
        float texelSize = ShadowParams.x;
        for (int x = -pcfRadius; x <= pcfRadius; x++)
        {
            for (int y = -pcfRadius; y <= pcfRadius; y++)
            {
                vec2 offset = vec2(x, y) * texelSize;
                float pcfDepth = texture(ShadowMapTexture, shadowCoord.xy + offset).r;
                shadowSum += (shadowCoord.z - bias > pcfDepth) ? 1.0 : 0.0;
            }
        }
        return 1.0 - shadowSum / float(taps * taps);
    }
    else
    {
        float depth = texture(ShadowMapTexture, shadowCoord.xy).r;
        return (shadowCoord.z - bias > depth) ? 0.0 : 1.0;
    }
}

void main()
{
    vec3 n = normalize(vNormal);
    vec3 l = normalize(-LightDirection.xyz);
    float ndotl = max(dot(n, l), 0.0);

    float shadow = SampleShadow(vWorldPos);

    float ao = 1.0;
    if (ScreenParams.z > 0.0)
    {
        vec2 screenUV = gl_FragCoord.xy / ScreenParams.xy;
        ao = texture(AOTexture, screenUV).r;
    }

    vec3 lighting = AmbientColor.rgb + LightColor.rgb * LightColor.a * ndotl * shadow * ao;

    vec3 v = normalize(CameraPosition.xyz - vWorldPos.xyz);
    vec3 h = normalize(l + v);
    float ndoth = max(dot(n, h), 0.0);
    vec3 specular = LightColor.rgb * LightColor.a * pow(ndoth, SpecularParams.y) * SpecularParams.x;

    vec4 texColor = texture(BaseColorTexture, vUV);
    vec4 albedo = BaseColor * vColor * texColor;

    oColor = vec4(lighting * albedo.rgb + specular, albedo.a);
}
