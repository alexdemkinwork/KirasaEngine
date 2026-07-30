#version 460

layout(location = 0) in vec3 vNormal;
layout(location = 1) in vec4 vColor;
layout(location = 2) in vec2 vUV;
layout(location = 3) in vec4 vWorldPos;

layout(std140, set = 0, binding = 0) uniform FrameConstants
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

layout(std140, set = 0, binding = 1) uniform DrawConstants
{
    vec4 BaseColor;
    vec4 SpecularParams;
};

// Vulkan descriptor set bindings are one flat number space per set (unlike GL/D3D, where uniform-buffer
// and texture-unit/register namespaces are independent) — binding 0/1 are already taken by the two UBOs
// above, so the texture and sampler get their own separate bindings (2, 3) instead of reusing 0. Modeled
// as separate texture2D/sampler (not a combined sampler2D) so each maps 1:1 onto one ResourceLayoutElement,
// same as every other backend.
layout(set = 0, binding = 2) uniform texture2D BaseColorTexture;
layout(set = 0, binding = 3) uniform sampler BaseColorSampler;
layout(set = 0, binding = 4) uniform texture2D ShadowMapTexture;
layout(set = 0, binding = 5) uniform sampler ShadowMapSampler;
layout(set = 0, binding = 6) uniform texture2D AOTexture;
layout(set = 0, binding = 7) uniform sampler AOSampler;

layout(location = 0) out vec4 oColor;

void main()
{
    vec3 n = normalize(vNormal);
    vec3 l = normalize(-LightDirection.xyz);
    float ndotl = max(dot(n, l), 0.0);

    vec4 texColor = texture(sampler2D(BaseColorTexture, BaseColorSampler), vUV);
    vec4 albedo = BaseColor * vColor * texColor;

    float shadow = 1.0;
    if (ShadowParams.w > 0.5)
    {
        vec4 shadowCoord = LightViewProjection * vWorldPos;
        shadowCoord.xyz /= shadowCoord.w;
        shadowCoord.xy = shadowCoord.xy * 0.5 + 0.5;
        float depth = texture(sampler2D(ShadowMapTexture, ShadowMapSampler), shadowCoord.xy).r;
        float bias = ShadowParams.y;
        int radius = int(ShadowParams.z);
        if (radius > 0)
        {
            float shadowSum = 0.0;
            int taps = radius * 2 + 1;
            float texelSize = ShadowParams.x;
            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    vec2 offset = vec2(x, y) * texelSize;
                    float sampleDepth = texture(sampler2D(ShadowMapTexture, ShadowMapSampler), shadowCoord.xy + offset).r;
                    shadowSum += (shadowCoord.z - bias > sampleDepth) ? 1.0 : 0.0;
                }
            }
            shadow = 1.0 - shadowSum / float(taps * taps);
        }
        else
        {
            shadow = (shadowCoord.z - bias > depth) ? 0.0 : 1.0;
        }
    }

    float ao = 1.0;
    if (ScreenParams.z > 0.5)
    {
        vec2 screenUV = gl_FragCoord.xy / ScreenParams.xy;
        ao = texture(sampler2D(AOTexture, AOSampler), screenUV).r;
    }

    vec3 lighting = AmbientColor.rgb + LightColor.rgb * LightColor.a * ndotl * shadow * ao;

    vec3 v = normalize(CameraPosition.xyz - vWorldPos.xyz);
    vec3 h = normalize(l + v);
    float ndoth = max(dot(n, h), 0.0);
    vec3 specular = LightColor.rgb * LightColor.a * pow(ndoth, SpecularParams.y) * SpecularParams.x;

    oColor = vec4(lighting * albedo.rgb + specular, albedo.a);
}
