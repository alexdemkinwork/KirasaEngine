#version 460 core

layout(location = 0) in vec2 vUV;

layout(std140, binding = 0) uniform CompositeConstants
{
    vec4 Params0; // x = bloomIntensity, y = vignetteIntensity, z = saturation, w = contrast
    vec4 Params1; // x = bloomEnabled (0/1), y = vignetteEnabled (0/1), z/w unused
};

layout(binding = 0) uniform sampler2D HdrColorTexture;
layout(binding = 1) uniform sampler2D BloomTexture;

layout(location = 0) out vec4 oColor;

// ACES filmic tone mapping
vec3 AcesTonemap(vec3 color)
{
    const float a = 2.51;
    const float b = 0.03;
    const float c = 2.43;
    const float d = 0.59;
    const float e = 0.14;
    return clamp((color * (a * color + b)) / (color * (c * color + d) + e), 0.0, 1.0);
}

// Vignette effect
float Vignette(vec2 uv, float intensity)
{
    vec2 center = vec2(0.5, 0.5);
    float dist = distance(uv, center) * 1.4142; // sqrt(2)
    return 1.0 - smoothstep(0.3, 0.9, dist) * intensity;
}

// Saturation/contrast adjustment
vec3 ColorGrade(vec3 color, float saturation, float contrast)
{
    float luminance = dot(color, vec3(0.2126, 0.7152, 0.0722));
    vec3 saturated = mix(vec3(luminance), color, saturation);
    return (saturated - 0.5) * contrast + 0.5;
}

void main()
{
    vec3 hdrColor = texture(HdrColorTexture, vUV).rgb;
    
    // Add bloom
    if (Params1.x > 0.5)
    {
        vec3 bloom = texture(BloomTexture, vUV).rgb;
        hdrColor += bloom * Params0.x;
    }

    // Tonemap
    vec3 tonemapped = AcesTonemap(hdrColor);

    // Color grading
    vec3 graded = ColorGrade(tonemapped, Params0.z, Params0.w);

    // Vignette
    if (Params1.y > 0.5)
    {
        float vig = Vignette(vUV, Params0.y);
        graded *= vig;
    }

    // Gamma correction (approximate sRGB)
    vec3 finalColor = pow(graded, vec3(1.0 / 2.2));

    oColor = vec4(finalColor, 1.0);
}