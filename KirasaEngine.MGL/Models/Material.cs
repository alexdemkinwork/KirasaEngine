namespace KirasaEngine.MGL.Models;

public sealed class Material
{
    /// <summary>Key into <see cref="Rendering.ShaderLibrary.ShaderLibrary"/>.</summary>
    public string ShaderName { get; set; } = "Standard";
    public Vector4 BaseColor { get; set; } = Vector4.One;
    public ITexture? BaseColorTexture { get; set; }
    public BlendMode Blend { get; set; } = BlendMode.Opaque;
    public bool DoubleSided { get; set; }
    public float SpecularIntensity { get; set; } = 0.5f;
    public float Shininess { get; set; } = 32f;
}
