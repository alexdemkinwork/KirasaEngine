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
    // Construct world matrix from row-major data (rows are stored as vec4 in instance data)
    // In GLSL, matrices are column-major, so we need to transpose or construct carefully
    mat4 world = mat4(
        vec4(iInstanceWorld0.x, iInstanceWorld1.x, iInstanceWorld2.x, iInstanceWorld3.x),
        vec4(iInstanceWorld0.y, iInstanceWorld1.y, iInstanceWorld2.y, iInstanceWorld3.y),
        vec4(iInstanceWorld0.z, iInstanceWorld1.z, iInstanceWorld2.z, iInstanceWorld3.z),
        vec4(iInstanceWorld0.w, iInstanceWorld1.w, iInstanceWorld2.w, iInstanceWorld3.w)
    );
    vec4 worldPos = world * vec4(iPosition, 1.0);

    gl_Position = ViewProjection * worldPos;
    vNormal = mat3(world) * iNormal;
    vColor = iColor * iInstanceColor;
    vUV = iUV;
    vWorldPos = worldPos;
}
