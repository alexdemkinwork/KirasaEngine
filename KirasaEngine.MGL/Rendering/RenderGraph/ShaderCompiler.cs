using System;

using KirasaEngine.MGL.Rendering;

namespace KirasaEngine.MGL.Rendering.RenderGraph;

/// <summary>
/// Compiles shaders for all backends from a single source.
/// </summary>
public class ShaderCompiler
{
    private readonly IGraphicsDevice _device;
    private readonly Dictionary<string, IShaderSet> _shaderCache = new();
    
    /// <summary>
    /// Initializes a new instance of the <see cref="ShaderCompiler"/> class.
    /// </summary>
    /// <param name="device">The graphics device.</param>
    public ShaderCompiler(IGraphicsDevice device)
    {
        _device = device;
    }
    
    /// <summary>
    /// Compiles a shader set for the given shader name and vertex layouts.
    /// </summary>
    /// <param name="shaderName">The name of the shader.</param>
    /// <param name="vertexLayouts">The vertex layouts.</param>
    /// <returns>The compiled shader set.</returns>
    public IShaderSet CompileShaderSet(string shaderName, IReadOnlyList<VertexLayoutDescription> vertexLayouts)
    {
        var key = $"{shaderName}_{string.Join("_", vertexLayouts)}";
        if (_shaderCache.TryGetValue(key, out var shaderSet))
            return shaderSet;
        
        shaderSet = _device.Factory.CreateShaderSet(new ShaderSetDescription
        {
            ShaderName = shaderName,
            VertexLayouts = vertexLayouts.ToArray(),
        });
        
        _shaderCache[key] = shaderSet;
        return shaderSet;
    }
    
    /// <summary>
    /// Compiles a shader set for the given shader name, variant, and vertex layouts.
    /// </summary>
    /// <param name="shaderName">The name of the shader.</param>
    /// <param name="variant">The shader variant.</param>
    /// <param name="vertexLayouts">The vertex layouts.</param>
    /// <returns>The compiled shader set.</returns>
    public IShaderSet CompileShaderSet(string shaderName, string variant, IReadOnlyList<VertexLayoutDescription> vertexLayouts)
    {
        return CompileShaderSet($"{shaderName}_{variant}", vertexLayouts);
    }
}