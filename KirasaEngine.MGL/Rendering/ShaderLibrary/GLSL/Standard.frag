#version 460 core

layout(location = 0) in vec3 vNormal;
layout(location = 1) in vec4 vColor;
layout(location = 2) in vec2 vUV;

layout(std140, binding = 0) uniform FrameConstants
{
    mat4 ViewProjection;
    vec4 LightDirection;
    vec4 LightColor;
    vec4 AmbientColor;
};

layout(std140, binding = 1) uniform DrawConstants
{
    vec4 BaseColor;
};

layout(binding = 0) uniform sampler2D BaseColorTexture;

layout(location = 0) out vec4 oColor;

void main()
{
    vec3 n = normalize(vNormal);
    vec3 l = normalize(-LightDirection.xyz);
    float ndotl = max(dot(n, l), 0.0);
    vec3 lighting = AmbientColor.rgb + LightColor.rgb * LightColor.a * ndotl;

    vec4 texColor = texture(BaseColorTexture, vUV);
    vec4 albedo = BaseColor * vColor * texColor;

    oColor = vec4(lighting * albedo.rgb, albedo.a);
}
