namespace KirasaEngine.MGL.Rendering;

/// <summary>Shared quality tier for every post-process effect. <see cref="Off"/> skips the effect's pass(es) entirely.</summary>
public enum RenderQuality
{
    Off,
    Low,
    Medium,
    High,
    VeryHigh,
    Ultra,
}

/// <summary>
/// Drives <see cref="SceneRenderer"/>'s optional passes (shadow map, SSAO, bloom, FXAA, vignette/color grade).
/// Each effect's own <see cref="RenderQuality"/> both toggles it (via <see cref="RenderQuality.Off"/>) and
/// scales its cost/quality (shadow map resolution, SSAO sample count, bloom blur radius, ...) — one tier
/// controls both, matching how a game's graphics-options menu usually presents this.
/// </summary>
public sealed class PostProcessSettings
{
    public RenderQuality ShadowQuality { get; set; } = RenderQuality.High;
    public RenderQuality SSAOQuality { get; set; } = RenderQuality.High;
    public RenderQuality BloomQuality { get; set; } = RenderQuality.High;
    public RenderQuality FXAAQuality { get; set; } = RenderQuality.High;
    public RenderQuality VignetteQuality { get; set; } = RenderQuality.Medium;

    /// <summary>Depth bias in light-clip-space units, applied in the shadow-sampling shader to fight acne.</summary>
    public float ShadowBias { get; set; } = 0.0025f;

    /// <summary>World-space half-extent padding added around the scene's node-position bounds when framing the shadow frustum.</summary>
    public float ShadowFrustumPadding { get; set; } = 3f;

    public float SSAORadius { get; set; } = 0.5f;
    public float SSAOPower { get; set; } = 1.6f;

    public float BloomThreshold { get; set; } = 1f;
    public float BloomIntensity { get; set; } = 0.55f;

    public float VignetteIntensity { get; set; } = 0.35f;
    public float Saturation { get; set; } = 1f;
    public float Contrast { get; set; } = 1f;

    public static PostProcessSettings Default => new();

    /// <summary>Sets every effect to the same tier at once (a graphics-options "preset" dropdown).</summary>
    public void SetOverallQuality(RenderQuality quality)
    {
        ShadowQuality = quality;
        SSAOQuality = quality;
        BloomQuality = quality;
        FXAAQuality = quality;
        VignetteQuality = quality;
    }

    public bool ShadowsActive => ShadowQuality != RenderQuality.Off;
    public bool SSAOActive => SSAOQuality != RenderQuality.Off;
    public bool BloomActive => BloomQuality != RenderQuality.Off;
    public bool FXAAActive => FXAAQuality != RenderQuality.Off;
    public bool VignetteActive => VignetteQuality != RenderQuality.Off;

    public uint ShadowMapResolution => ShadowQuality switch
    {
        RenderQuality.Off => 0,
        RenderQuality.Low => 512,
        RenderQuality.Medium => 1024,
        RenderQuality.High => 2048,
        RenderQuality.VeryHigh => 3072,
        RenderQuality.Ultra => 4096,
        _ => 2048,
    };

    /// <summary>PCF kernel half-width in taps per axis: 0 = single tap, 1 = 3x3, 2 = 5x5.</summary>
    public int ShadowPcfRadius => ShadowQuality switch
    {
        RenderQuality.Off => 0,
        RenderQuality.Low => 0,
        RenderQuality.Medium => 1,
        RenderQuality.High => 1,
        RenderQuality.VeryHigh => 2,
        RenderQuality.Ultra => 2,
        _ => 1,
    };

    /// <summary>How many of the shared 48-entry hemisphere kernel each SSAO shader consumes.</summary>
    public int SSAOSampleCount => SSAOQuality switch
    {
        RenderQuality.Off => 0,
        RenderQuality.Low => 8,
        RenderQuality.Medium => 16,
        RenderQuality.High => 24,
        RenderQuality.VeryHigh => 32,
        RenderQuality.Ultra => 48,
        _ => 24,
    };

    /// <summary>Separable-blur half-radius in texels (full tap count is 2*radius+1 per axis).</summary>
    public int BloomBlurRadius => BloomQuality switch
    {
        RenderQuality.Off => 0,
        RenderQuality.Low => 3,
        RenderQuality.Medium => 5,
        RenderQuality.High => 7,
        RenderQuality.VeryHigh => 9,
        RenderQuality.Ultra => 12,
        _ => 7,
    };
}
