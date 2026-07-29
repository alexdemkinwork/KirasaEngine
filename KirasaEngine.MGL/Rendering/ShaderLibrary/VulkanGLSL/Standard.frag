#version 460

layout(location = 0) in vec3 vNormal;
layout(location = 1) in vec4 vColor;
layout(location = 2) in vec2 vUV;

layout(std140, set = 0, binding = 0) uniform FrameConstants
{
    mat4 ViewProjection;
    vec4 LightDirection;
    vec4 LightColor;
    vec4 AmbientColor;
};

layout(std140, set = 0, binding = 1) uniform DrawConstants
{
    vec4 BaseColor;
};

// Vulkan descriptor set bindings are one flat number space per set (unlike GL/D3D, where uniform-buffer
// and texture-unit/register namespaces are independent) — binding 0/1 are already taken by the two UBOs
// above, so the texture and sampler get their own separate bindings (2, 3) instead of reusing 0. Modeled
// as separate texture2D/sampler (not a combined sampler2D) so each maps 1:1 onto one ResourceLayoutElement,
// same as every other backend.
layout(set = 0, binding = 2) uniform texture2D BaseColorTexture;
layout(set = 0, binding = 3) uniform sampler BaseColorSampler;

layout(location = 0) out vec4 oColor;

void main()
{
    vec3 n = normalize(vNormal);
    vec3 l = normalize(-LightDirection.xyz);
    float ndotl = max(dot(n, l), 0.0);
    vec3 lighting = AmbientColor.rgb + LightColor.rgb * LightColor.a * ndotl;

    vec4 texColor = texture(sampler2D(BaseColorTexture, BaseColorSampler), vUV);
    vec4 albedo = BaseColor * vColor * texColor;

    oColor = vec4(lighting * albedo.rgb, albedo.a);
}
