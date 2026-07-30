#version 460 core

layout(location = 0) in vec2 vUV;

layout(set = 0, binding = 0) uniform FXAAConstants
{
    vec4 Params0; // x = texelSizeX, y = texelSizeY, z/w unused
};

layout(set = 0, binding = 1) uniform texture2D SourceTexture;
layout(set = 0, binding = 2) uniform sampler SourceSampler;

layout(location = 0) out vec4 oColor;

// FXAA 3.11 implementation (based on Timothy Lottes' public domain code)
#define FXAA_REDUCE_MIN   (1.0/128.0)
#define FXAA_REDUCE_MUL   (1.0/8.0)
#define FXAA_SPAN_MAX     8.0

float luma(vec3 c) { return dot(c, vec3(0.299, 0.587, 0.114)); }

void main()
{
    vec2 texelSize = vec2(Params0.x, Params0.y);
    vec2 invTexelSize = 1.0 / texelSize;

    // Sample the center pixel and neighbors
    vec3 rgbNW = texture(sampler2D(SourceTexture, SourceSampler), vUV + texelSize * vec2(-1.0, -1.0)).rgb;
    vec3 rgbNE = texture(sampler2D(SourceTexture, SourceSampler), vUV + texelSize * vec2(1.0, -1.0)).rgb;
    vec3 rgbSW = texture(sampler2D(SourceTexture, SourceSampler), vUV + texelSize * vec2(-1.0, 1.0)).rgb;
    vec3 rgbSE = texture(sampler2D(SourceTexture, SourceSampler), vUV + texelSize * vec2(1.0, 1.0)).rgb;
    vec3 rgbM  = texture(sampler2D(SourceTexture, SourceSampler), vUV).rgb;
    vec3 rgbN  = texture(sampler2D(SourceTexture, SourceSampler), vUV + texelSize * vec2(0.0, -1.0)).rgb;
    vec3 rgbS  = texture(sampler2D(SourceTexture, SourceSampler), vUV + texelSize * vec2(0.0, 1.0)).rgb;
    vec3 rgbW  = texture(sampler2D(SourceTexture, SourceSampler), vUV + texelSize * vec2(-1.0, 0.0)).rgb;
    vec3 rgbE  = texture(sampler2D(SourceTexture, SourceSampler), vUV + texelSize * vec2(1.0, 0.0)).rgb;

    float lumaNW = luma(rgbNW);
    float lumaNE = luma(rgbNE);
    float lumaSW = luma(rgbSW);
    float lumaSE = luma(rgbSE);
    float lumaM  = luma(rgbM);
    float lumaN  = luma(rgbN);
    float lumaS  = luma(rgbS);
    float lumaW  = luma(rgbW);
    float lumaE  = luma(rgbE);

    // Find the min/max luminance in the neighborhood
    float lumaMin = min(lumaM, min(min(lumaN, lumaS), min(lumaW, lumaE)));
    float lumaMax = max(lumaM, max(max(lumaN, lumaS), max(lumaW, lumaE)));

    // Edge detection
    float edgeN = abs(lumaN - lumaM);
    float edgeS = abs(lumaS - lumaM);
    float edgeW = abs(lumaW - lumaM);
    float edgeE = abs(lumaE - lumaM);

    float edgeNS = edgeN + edgeS;
    float edgeEW = edgeE + edgeW;

    // Choose direction
    bool horizontal = edgeNS >= edgeEW;

    float luma1 = horizontal ? lumaN : lumaW;
    float luma2 = horizontal ? lumaS : lumaE;
    float luma3 = horizontal ? lumaNW : lumaSW;
    float luma4 = horizontal ? lumaNE : lumaSE;

    float gradient1 = abs(luma1 - lumaM);
    float gradient2 = abs(luma2 - lumaM);

    // Determine edge direction
    float step = (gradient1 < gradient2) ? -1.0 : 1.0;
    
    // Calculate the offset
    float offset = 0.0;
    float lumaEnd1 = horizontal ? lumaN : lumaW;
    float lumaEnd2 = horizontal ? lumaS : lumaE;
    
    for (int i = 0; i < 3; i++)
    {
        float lumaSample1 = horizontal ? 
            texture(sampler2D(SourceTexture, SourceSampler), vUV + texelSize * vec2(0.0, -1.0 - float(i) * step)).r :
            texture(sampler2D(SourceTexture, SourceSampler), vUV + texelSize * vec2(-1.0 - float(i) * step, 0.0)).r;
        float lumaSample2 = horizontal ? 
            texture(sampler2D(SourceTexture, SourceSampler), vUV + texelSize * vec2(0.0, 1.0 + float(i) * step)).r :
            texture(sampler2D(SourceTexture, SourceSampler), vUV + texelSize * vec2(1.0 + float(i) * step, 0.0)).r;
        
        if (abs(lumaSample1 - lumaM) < abs(lumaEnd1 - lumaM) &&
            abs(lumaSample2 - lumaM) < abs(lumaEnd2 - lumaM))
        {
            offset = float(i + 1) * step;
        }
    }

    // Final sample position
    vec2 sampleUV = vUV;
    if (horizontal)
        sampleUV.y += offset * texelSize.y;
    else
        sampleUV.x += offset * texelSize.x;

    // Clamp to edge
    sampleUV = clamp(sampleUV, texelSize * 0.5, 1.0 - texelSize * 0.5);

    // Output the filtered color
    vec3 finalColor = texture(sampler2D(SourceTexture, SourceSampler), sampleUV).rgb;
    
    // Subpixel aliasing removal
    float lumaFinal = luma(finalColor);
    if (lumaFinal < lumaMin || lumaFinal > lumaMax)
        finalColor = rgbM;

    oColor = vec4(finalColor, 1.0);
}