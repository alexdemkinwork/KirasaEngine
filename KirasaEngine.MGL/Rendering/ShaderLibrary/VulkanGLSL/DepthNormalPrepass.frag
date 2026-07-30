#version 460 core

layout(location = 0) in vec3 vViewNormal;
layout(location = 1) in float vViewDepth;

layout(location = 0) out vec4 oNormalDepth;

void main()
{
    oNormalDepth = vec4(normalize(vViewNormal), vViewDepth);
}