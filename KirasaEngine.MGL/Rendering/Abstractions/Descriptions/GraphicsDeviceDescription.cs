namespace KirasaEngine.MGL.Rendering.Abstractions.Descriptions;

/// <summary>
/// <see cref="Window"/> is optional: when null the device is created headless (no swap chain), suitable for
/// pure render-to-texture usage. OpenGL always needs a window to host its context.
/// </summary>
public sealed class GraphicsDeviceDescription
{
    public required IWindow? Window { get; init; }
    public required uint Width { get; init; }
    public required uint Height { get; init; }
    public bool Debug { get; init; }
}
