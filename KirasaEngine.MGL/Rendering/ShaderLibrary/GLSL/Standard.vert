#version 460 core

layout(location = 0) in vec3 iPosition;
layout(location = 1) in vec3 iNormal;
layout(location = 2) in vec4 iColor;
layout(location = 3) in vec2 iUV;
layout(location = 4) in vec4 iInstanceWorld0;
layout(location = 5) in vec4 iInstanceWorld1;
layout(location = 6) in vec4 iInstanceWorld2;
layout(location = 7) in vec4 iInstanceWorld3;
layout(location = 8) in vec4 iInstanceColor;

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

layout(location = 0) out vec3 vNormal;
layout(location = 1) out vec4 vColor;
layout(location = 2) out vec2 vUV;
layout(location = 3) out vec4 vWorldPos;

void main()
{
    mat4 world = mat4(iInstanceWorld0, iInstanceWorld1, iInstanceWorld2, iInstanceWorld3);
    vec4 worldPos = world * vec4(iPosition, 1.0);

    gl_Position = ViewProjection * worldPos;
    vNormal = mat3(world) * iNormal;
    vColor = iColor * iInstanceColor;
    vUV = iUV;
    vWorldPos = worldPos;
}
