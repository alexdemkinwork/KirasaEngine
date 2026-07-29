namespace KirasaEngine.MGL.Rendering.Backends.Direct3D12;

/// <summary>
/// A colour <see cref="D3D12Texture"/> (created with an RTV) plus an optional depth texture (created with a
/// DSV). There is no framebuffer object to build as in OpenGL and no view pair to cache as in D3D11 — the
/// attachments are just handed to <c>OMSetRenderTargets</c> as descriptor handles.
/// </summary>
internal sealed class D3D12RenderTarget : IRenderTarget
{
    public uint Width { get; }
    public uint Height { get; }
    public TextureFormat ColorFormat { get; }
    public ITexture ColorTexture { get; }
    public ITexture? DepthTexture { get; }

    public D3D12Texture Color => (D3D12Texture)ColorTexture;
    public D3D12Texture? Depth => DepthTexture as D3D12Texture;

    public D3D12RenderTarget(D3D12GraphicsDevice device, in RenderTargetDescription description)
    {
        Width = description.Width;
        Height = description.Height;
        ColorFormat = description.ColorFormat;

        ColorTexture = new D3D12Texture(
            device,
            new TextureDescription(Width, Height, ColorFormat, TextureUsage.RenderTarget | TextureUsage.Sampled),
            default);

        if (description.DepthFormat is { } depthFormat)
        {
            DepthTexture = new D3D12Texture(
                device,
                new TextureDescription(Width, Height, depthFormat, TextureUsage.DepthStencil),
                default);
        }
    }

    public void Dispose()
    {
        ColorTexture.Dispose();
        DepthTexture?.Dispose();
    }
}
