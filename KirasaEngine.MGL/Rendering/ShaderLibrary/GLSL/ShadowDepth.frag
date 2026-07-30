#version 460 core

layout(location = 0) out float oDepth;

void main()
{
    oDepth = gl_FragCoord.z * 2.0 - 1.0;
}
