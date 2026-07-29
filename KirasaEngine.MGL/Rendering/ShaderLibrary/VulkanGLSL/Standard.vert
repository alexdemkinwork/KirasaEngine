#version 460

layout(location = 0) in vec3 iPosition;
layout(location = 1) in vec3 iNormal;
layout(location = 2) in vec4 iColor;
layout(location = 3) in vec2 iUV;
layout(location = 4) in vec4 iInstanceWorld0;
layout(location = 5) in vec4 iInstanceWorld1;
layout(location = 6) in vec4 iInstanceWorld2;
layout(location = 7) in vec4 iInstanceWorld3;
layout(location = 8) in vec4 iInstanceColor;

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

layout(location = 0) out vec3 vNormal;
layout(location = 1) out vec4 vColor;
layout(location = 2) out vec2 vUV;

void main()
{
    // Instance rows arrive already column-major (see SceneRenderer packing) so a plain mat4*vec4 is correct here.
    // NDC Y-flip for Vulkan's downward-pointing clip-space Y axis is handled by the backend via a
    // negative-height viewport, not here — this shader is intentionally identical in spirit to the GL one.
    mat4 world = mat4(iInstanceWorld0, iInstanceWorld1, iInstanceWorld2, iInstanceWorld3);
    vec4 worldPos = world * vec4(iPosition, 1.0);

    gl_Position = ViewProjection * worldPos;
    vNormal = mat3(world) * iNormal;
    vColor = iColor * iInstanceColor;
    vUV = iUV;
}
