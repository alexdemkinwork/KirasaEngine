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

layout(std140, binding = 0) uniform PrepassConstants
{
    mat4 ViewProjection;
    mat4 View;
};

layout(location = 0) out vec3 vViewNormal;
layout(location = 1) out float vViewDepth;

void main()
{
    mat4 world = mat4(iInstanceWorld0, iInstanceWorld1, iInstanceWorld2, iInstanceWorld3);
    vec4 worldPos = world * vec4(iPosition, 1.0);
    vec4 viewPos = View * worldPos;

    gl_Position = ViewProjection * worldPos;
    vViewNormal = normalize(mat3(View) * mat3(world) * iNormal);
    // Camera looks down local -Z in view space, so points in front have negative Z; store a positive distance.
    vViewDepth = -viewPos.z;
}
