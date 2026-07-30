#version 460 core

layout(location = 0) in vec2 vUV;

layout(set = 0, binding = 0) uniform CompositeConstants
{
    vec4 Params0; // x = bloomIntensity, y = vignetteIntensity, z = saturation, w = contrast
    vec4 Params1; // x = bloomEnabled, y = vignetteEnabled, z/w unused
};

layout(set = 0, binding = 1) uniform texture2D HdrColorTexture;
layout(set = 0, binding = 2) uniform sampler HdrColorSampler;
layout(set = 0, binding = 3) uniform texture2D BloomTexture;
layout(set = 0, binding = 4) uniform sampler BloomSampler;

layout(location = 0) out vec4 oColor;

// ACES filmic tonemapping
vec3 ACESFilm(vec3 x)
{
    float a = 2.51;
    float b = 0.03;
    float c = 2.43;
    float d = 0.59;
    float e = 0.14;
    return clamp((x * (a * x + b)) / (x * (c * x + d) + e), 0.0, 1.0);
}

// sRGB encode
vec3 SRGBEncode(vec3 c)
{
    return pow(c, vec3(1.0 / 2.2));
}

void main()
{
    vec3 hdrColor = texture(sampler2D(HdrColorTexture, HdrColorSampler), vUV).rgb;
    
    // Bloom
    float bloomEnabled = Params1.x;
    if (bloomEnabled > 0.5)
    {
        vec3 bloomColor = texture(sampler2D(BloomTexture, BloomSampler), vUV).rgb;
        hdrColor += bloomColor * Params0.x;
    }

    // Tonemap
    vec3 tonemapped = ACESFilm(hdrColor);

    // Saturation/Contrast
    float saturation = Params0.z;
    float contrast = Params0.w;
    vec3 luminance = vec3(0.2126, 0.7152, 0.0722);
    float l = dot(tonemapped, luminance);
    tonemapped = mix(vec3(l), tonemapped, saturation);
    tonemapped = mix(vec3(0.5), tonemapped, contrast);

    // Vignette
    float vignetteEnabled = Params1.y;
    if (vignetteEnabled > 0.5)
    {
        vec2 center = vUV - 0.5;
        float dist = length(center) * 2.0;
        float vignette = 1.0 - smoothstep(0.0, 1.0, dist) * Params0.y;
        tonemapped *= vignette;
    }

    // sRGB encode
    tonemapped = SRGBEncode(tonemapped);

    oColor = vec4(tonemapped, 1.0);
}