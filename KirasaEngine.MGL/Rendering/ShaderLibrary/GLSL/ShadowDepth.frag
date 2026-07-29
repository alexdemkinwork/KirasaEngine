#version 460 core

layout(location = 0) out float oDepth;

// Stores the native [0,1] post-projection depth as a plain color value in an R32Float target, sidestepping
// every backend's separate "sampleable depth attachment" plumbing — the main pass samples this like any
// other texture. gl_FragCoord.z is exactly the depth a real depth attachment would have stored here.
void main()
{
    oDepth = gl_FragCoord.z;
}
