namespace KirasaEngine.MGL.Rendering.Abstractions;

public interface IGraphicsDevice : IDisposable
{
    GraphicsBackend Backend { get; }
    IResourceFactory Factory { get; }
    uint Width { get; }
    uint Height { get; }

    void Initialize(GraphicsDeviceDescription description);

    ICommandList CreateCommandList();

    /// <summary>Records are already flushed to completion by the time this returns (see plan's synchronous-submit simplification).</summary>
    void Submit(ICommandList commandList);

    /// <summary>Presents the swap chain backbuffer. No-op if the device was created headless.</summary>
    void Present();

    void Resize(uint width, uint height);

    /// <summary>Synchronously reads back a render target's color attachment as tightly packed, top-left-origin RGBA8 bytes.</summary>
    byte[] ReadRenderTarget(IRenderTarget target);
}
