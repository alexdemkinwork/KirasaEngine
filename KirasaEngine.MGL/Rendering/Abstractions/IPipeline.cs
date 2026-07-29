namespace KirasaEngine.MGL.Rendering.Abstractions;

public interface IPipeline : IDisposable
{
    PipelineDescription Description { get; }
}
