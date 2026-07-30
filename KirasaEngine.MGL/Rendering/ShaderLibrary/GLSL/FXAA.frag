#version 460 core

layout(location = 0) in vec2 vUV;

layout(std140, binding = 0) uniform FXAAConstants
{
    vec4 Params0; // x = texelSizeX, y = texelSizeY, z/w unused
};

layout(binding = 0) uniform sampler2D SourceTexture;

layout(location = 0) out vec4 oColor;

// FXAA 3.11 implementation (simplified, single-pass)
#define FXAA_REDUCE_MIN   (1.0/128.0)
#define FXAA_REDUCE_MUL   (1.0/8.0)
#define FXAA_SPAN_MAX     8.0

void main()
{
    vec2 texelSize = vec2(Params0.x, Params0.y);
    vec2 uv = vUV;
    vec3 rgbNW = texture(SourceTexture, uv + vec2(-texelSize.x, -texelSize.y)).rgb;
    vec3 rgbNE = texture(SourceTexture, uv + vec2( texelSize.x, -texelSize.y)).rgb;
    vec3 rgbSW = texture(SourceTexture, uv + vec2(-texelSize.x,  texelSize.y)).rgb;
    vec3 rgbSE = texture(SourceTexture, uv + vec2( texelSize.x,  texelSize.y)).rgb;
    vec3 rgbM  = texture(SourceTexture, uv).rgb;

    // Luminance
    float lumaNW = dot(rgbNW, vec3(0.299, 0.587, 0.114));
    float lumaNE = dot(rgbNE, vec3(0.299, 0.587, 0.114));
    float lumaSW = dot(rgbSW, vec3(0.299, 0.587, 0.114));
    float lumaSE = dot(rgbSE, vec3(0.299, 0.587, 0.114));
    float lumaM  = dot(rgbM,  vec3(0.299, 0.587, 0.114));

    float lumaMin = min(lumaM, min(min(lumaNW, lumaNE), min(lumaSW, lumaSE)));
    float lumaMax = max(lumaM, max(max(lumaNW, lumaNE), max(lumaSW, lumaSE)));

    vec2 dir;
    dir.x = -((lumaNW + lumaNE) - (lumaSW + lumaSE));
    dir.y =  ((lumaNW + lumaSW) - (lumaNE + lumaSE));

    float dirReduce = max((lumaNW + lumaNE + lumaSW + lumaSE) * (0.25 * FXAA_REDUCE_MUL), FXAA_REDUCE_MIN);

    float rcpDirMin = 1.0 / (min(abs(dir.x), abs(dir.y)) + dirReduce);
    dir = min(vec2(FXAA_SPAN_MAX, FXAA_SPAN_MAX),
              max(vec2(-FXAA_SPAN_MAX, -FXAA_SPAN_MAX),
              dir * rcpDirMin)) * texelSize;

    vec3 rgbA = 0.5 * (
        texture(SourceTexture, uv + dir * (1.0/3.0 - 0.5)).rgb +
        texture(SourceTexture, uv + dir * (2.0/3.0 - 0.5)).rgb);
    vec3 rgbB = rgbA * 0.5 + 0.25 * (
        texture(SourceTexture, uv + dir * -0.5).rgb +
        texture(SourceTexture, uv + dir *  0.5).rgb);

    float lumaB = dot(rgbB, vec3(0.299, 0.587, 0.114));
    if ((lumaB < lumaMin) || (lumaB > lumaMax))
        oColor = vec4(rgbA, 1.0);
    else
        oColor = vec4(rgbB, 1.0);
}