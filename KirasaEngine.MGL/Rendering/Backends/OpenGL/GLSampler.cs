using Silk.NET.OpenGL;

namespace KirasaEngine.MGL.Rendering.Backends.OpenGL;

internal sealed class GLSampler : ISampler
{
    private readonly GL _gl;
    public uint Handle { get; }

    public GLSampler(GL gl, in SamplerDescription description)
    {
        _gl = gl;
        Handle = _gl.GenSampler();
        _gl.SamplerParameter(Handle, SamplerParameterI.MinFilter, (int)GLFormats.MapMinFilter(description.Filter));
        _gl.SamplerParameter(Handle, SamplerParameterI.MagFilter, (int)GLFormats.MapMagFilter(description.Filter));
        var address = (int)GLFormats.MapAddressMode(description.AddressMode);
        _gl.SamplerParameter(Handle, SamplerParameterI.WrapS, address);
        _gl.SamplerParameter(Handle, SamplerParameterI.WrapT, address);
    }

    public void Dispose() => _gl.DeleteSampler(Handle);
}
