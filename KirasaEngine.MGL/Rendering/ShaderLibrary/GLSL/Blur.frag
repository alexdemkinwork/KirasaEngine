#version 460 core

layout(location = 0) in vec2 vUV;

layout(std140, binding = 0) uniform BlurConstants
{
    vec4 Params0; // x = texelSizeX, y = texelSizeY, z = 0/1 (horizontal/vertical), w = blurRadius
    vec4 Params1; // x = threshold, y = applyThreshold (0/1), z/w unused
};

layout(binding = 0) uniform sampler2D SourceTexture;

layout(location = 0) out vec4 oColor;

void main()
{
    float texelSize = Params0.z == 0.0 ? Params0.x : Params0.y; // horizontal or vertical
    float radius = Params0.w;
    float threshold = Params1.x;
    bool applyThreshold = Params1.y > 0.5;

    vec4 color = vec4(0.0);
    float weightSum = 0.0;

    // Gaussian-ish weights for separable blur
    // Using a simple box-like kernel for performance; can be upgraded to true Gaussian
    for (int i = int(-radius); i <= int(radius); i++)
    {
        float offset = float(i) * texelSize;
        vec2 sampleUV = Params0.z == 0.0 ? vec2(vUV.x + offset, vUV.y) : vec2(vUV.x, vUV.y + offset);

        if (sampleUV.x < 0.0 || sampleUV.x > 1.0 || sampleUV.y < 0.0 || sampleUV.y > 1.0)
            continue;

        vec4 sample = texture(SourceTexture, sampleUV);

        // Bright-pass threshold for the first (horizontal) pass
        if (applyThreshold)
        {
            float luminance = dot(sample.rgb, vec3(0.2126, 0.7152, 0.0722));
            if (luminance < threshold)
                continue;
        }

        // Simple uniform weight (can be precomputed Gaussian weights for quality)
        float weight = 1.0;
        color += sample * weight;
        weightSum += weight;
    }

    oColor = weightSum > 0.0 ? color / weightSum : vec4(0.0);
}