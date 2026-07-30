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

layout(set = 0, binding = 0) uniform ShadowConstants
{
    mat4 LightViewProjection;
};

void main()
{
    mat4 world = mat4(iInstanceWorld0, iInstanceWorld1, iInstanceWorld2, iInstanceWorld3);
    gl_Position = LightViewProjection * world * vec4(iPosition, 1.0);
}