#version 460 core

// With ClipControl(ZeroToOne) set, gl_FragCoord.z is in [0,1] range.
// We output this directly for depth comparison in shadow mapping.
layout(location = 0) out float oDepth;

void main()
{
    // gl_FragCoord.z is already in [0,1] due to ClipControl(ZeroToOne)
    // Output it directly for proper depth comparison
    oDepth = gl_FragCoord.z;
}
