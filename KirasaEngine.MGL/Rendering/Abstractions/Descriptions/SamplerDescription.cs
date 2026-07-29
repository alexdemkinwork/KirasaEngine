namespace KirasaEngine.MGL.Rendering.Abstractions.Descriptions;

public readonly struct SamplerDescription(
    SamplerFilter filter = SamplerFilter.Linear,
    SamplerAddressMode addressMode = SamplerAddressMode.Wrap)
{
    public readonly SamplerFilter Filter = filter;
    public readonly SamplerAddressMode AddressMode = addressMode;

    public static readonly SamplerDescription LinearWrap = new(SamplerFilter.Linear, SamplerAddressMode.Wrap);
    public static readonly SamplerDescription PointClamp = new(SamplerFilter.Point, SamplerAddressMode.Clamp);
}
