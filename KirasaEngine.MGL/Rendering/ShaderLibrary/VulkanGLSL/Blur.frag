#version 460 core

layout(location = 0) in vec2 vUV;

layout(set = 0, binding = 0) uniform BlurConstants
{
    vec4 Params0; // x = texelSizeX, y = texelSizeY, z = horizontal (0/1), w = blurRadius
    vec4 Params1; // x = threshold, y = applyThreshold (0/1), z/w unused
};

layout(set = 0, binding = 1) uniform texture2D SourceTexture;
layout(set = 0, binding = 2) uniform sampler SourceSampler;

layout(location = 0) out vec4 oColor;

void main()
{
    float horizontal = Params0.z;
    float radius = Params0.w;
    float threshold = Params1.x;
    bool applyThreshold = Params1.y > 0.5;

    vec2 texelSize = vec2(Params0.x, Params0.y);
    vec4 color = vec4(0.0);
    float weightSum = 0.0;

    // Gaussian-like weights for separable blur
    // Using a simple box-like kernel with center-weighted distribution
    for (int i = -4; i <= 4; i++)
    {
        float offset = float(i);
        float weight = exp(-0.5 * (offset * offset) / (radius * radius));
        
        vec2 sampleUV = vUV;
        if (horizontal > 0.5)
            sampleUV.x += offset * texelSize.x;
        else
            sampleUV.y += offset * texelSize.y;

        vec4 sampleColor = texture(sampler2D(SourceTexture, SourceSampler), sampleUV);
        
        // Bright-pass threshold for first (horizontal) pass
        if (applyThreshold)
        {
            float luminance = dot(sampleColor.rgb, vec3(0.2126, 0.7152, 0.0722));
            if (luminance < threshold)
                sampleColor = vec4(0.0);
        }

        color += sampleColor * weight;
        weightSum += weight;
    }

    oColor = color / weightSum;
}