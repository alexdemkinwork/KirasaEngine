using Silk.NET.OpenGL;

using KirasaEngine.MGL.Rendering;

namespace KirasaEngine.MGL.Rendering.Backends.OpenGL;

internal sealed class GLSampler : ISampler
{
    private readonly GL _gl;
    public uint Handle { get; }

    public GLSampler(GL gl, in SamplerDescription description)
    {
        _gl = gl;
        Handle = _gl.GenSampler();
        GLErrorChecker.ValidateHandle(Handle, "Sampler");
        GLErrorChecker.CheckError(_gl, "GenSampler");
        
        _gl.SamplerParameter(Handle, SamplerParameterI.MinFilter, (int)GLFormats.MapMinFilter(description.Filter));
        GLErrorChecker.CheckError(_gl, "SamplerParameter MinFilter");
        _gl.SamplerParameter(Handle, SamplerParameterI.MagFilter, (int)GLFormats.MapMagFilter(description.Filter));
        GLErrorChecker.CheckError(_gl, "SamplerParameter MagFilter");
        var address = (int)GLFormats.MapAddressMode(description.AddressMode);
        _gl.SamplerParameter(Handle, SamplerParameterI.WrapS, address);
        GLErrorChecker.CheckError(_gl, "SamplerParameter WrapS");
        _gl.SamplerParameter(Handle, SamplerParameterI.WrapT, address);
        GLErrorChecker.CheckError(_gl, "SamplerParameter WrapT");
    }

    public void Dispose()
    {
        GLErrorChecker.ValidateHandle(Handle, "Sampler");
        _gl.DeleteSampler(Handle);
        GLErrorChecker.CheckError(_gl, "DeleteSampler");
    }
}
