using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;
using Silk.NET.DXGI;

namespace KirasaEngine.MGL.Rendering.Backends.Direct3D11;

/// <summary>
/// Direct3D11 device. Mirrors <c>GLGraphicsDevice</c>: it owns the API entry point plus the one global
/// "context" every resource is created against, and hands both to the resource factory.
///
/// <para>Only the offscreen render-to-texture path is wired up (no swap chain) — that is the entire
/// contract <see cref="SceneRenderer"/> needs, and <see cref="Present"/> is documented as a no-op for a
/// headless device by <see cref="IGraphicsDevice"/> anyway.</para>
/// </summary>
public sealed unsafe class D3D11GraphicsDevice : IGraphicsDevice
{
    private D3D11 _d3d11 = null!;
    private ID3D11Device* _device;
    private ID3D11DeviceContext* _context;

    // Reused across frames: ReadRenderTarget is called once per RenderToTexture and re-creating a staging
    // texture every time would be pure waste.
    private ID3D11Texture2D* _stagingTexture;
    private uint _stagingWidth;
    private uint _stagingHeight;
    private TextureFormat _stagingFormat;

    public GraphicsBackend Backend => GraphicsBackend.Direct3D11;
    public IResourceFactory Factory { get; private set; } = null!;
    public uint Width { get; private set; }
    public uint Height { get; private set; }

    internal ID3D11Device* Device => _device;
    internal ID3D11DeviceContext* Context => _context;

    /// <summary>
    /// Throws on a failed HRESULT. Every D3D11 entry point in this backend funnels through here, mirroring
    /// <c>VulkanUtil.Check</c>.
    /// </summary>
    public static void Check(int result, string what)
    {
        if (result < 0)
            throw new InvalidOperationException($"Direct3D11 call failed ({what}): HRESULT 0x{result:X8}");
    }

    public void Initialize(GraphicsDeviceDescription description)
    {
        // The window (when supplied) is only ever used for its native handle; D3D11 needs no window at all
        // to render into an offscreen target, so a null window is perfectly valid here.
        Width = description.Width;
        Height = description.Height;

        // The window-taking overload is the non-obsolete one; it accepts null, which is exactly the
        // headless case.
        _d3d11 = D3D11.GetApi(description.Window);

        var flags = description.Debug ? (uint)CreateDeviceFlag.Debug : 0u;

        var hr = TryCreateDevice(D3DDriverType.Hardware, flags);

        // The D3D11 debug layer ships with the Graphics Tools optional feature; if it isn't installed the
        // creation fails outright, so drop the flag rather than failing the whole device.
        if (hr < 0 && flags != 0)
        {
            flags = 0;
            hr = TryCreateDevice(D3DDriverType.Hardware, flags);
        }

        // No hardware adapter (headless VM / CI box): WARP is a fully conformant software rasterizer and
        // produces identical images, which is all the smoke test cares about.
        if (hr < 0)
            hr = TryCreateDevice(D3DDriverType.Warp, flags);

        Check(hr, "D3D11CreateDevice");

        if (_device is null || _context is null)
            throw new InvalidOperationException("D3D11CreateDevice succeeded but returned a null device or context.");

        Factory = new D3D11ResourceFactory(_device, _context);
    }

    private int TryCreateDevice(D3DDriverType driverType, uint flags)
    {
        var featureLevels = stackalloc D3DFeatureLevel[2];
        featureLevels[0] = D3DFeatureLevel.Level111;
        featureLevels[1] = D3DFeatureLevel.Level110;

        ID3D11Device* device = null;
        ID3D11DeviceContext* context = null;

        var hr = _d3d11.CreateDevice(
            (IDXGIAdapter*)null,
            driverType,
            nint.Zero,
            flags,
            featureLevels,
            2u,
            (uint)D3D11.SdkVersion,
            &device,
            (D3DFeatureLevel*)null,
            &context);

        if (hr >= 0)
        {
            _device = device;
            _context = context;
        }
        else
        {
            if (context is not null) context->Release();
            if (device is not null) device->Release();
        }

        return hr;
    }

    public ICommandList CreateCommandList() => new D3D11CommandList(_context);

    public void Submit(ICommandList commandList)
    {
        // D3D11CommandList records straight into the immediate context, so everything has already been
        // handed to the driver by the time Submit is reached (the synchronous-submit simplification the
        // IGraphicsDevice.Submit doc comment describes). Flush only pushes the queued work out; the
        // subsequent Map(READ) in ReadRenderTarget is what actually blocks until the GPU is done.
        _context->Flush();
    }

    /// <summary>No-op: this backend is headless by design (see the class remarks) — there is no swap chain.</summary>
    public void Present() { }

    public void Resize(uint width, uint height)
    {
        Width = width;
        Height = height;
    }

    public byte[] ReadRenderTarget(IRenderTarget target)
    {
        var d3dTarget = (D3D11RenderTarget)target;
        var colorTexture = (D3D11Texture)d3dTarget.ColorTexture;
        var width = d3dTarget.Width;
        var height = d3dTarget.Height;
        var format = d3dTarget.ColorFormat;

        EnsureStagingTexture(width, height, format);

        // Render targets live in GPU-only memory; a staging copy is the only way to get at the bytes.
        _context->CopyResource((ID3D11Resource*)_stagingTexture, (ID3D11Resource*)colorTexture.Handle);

        var mapped = new MappedSubresource();
        Check(
            _context->Map((ID3D11Resource*)_stagingTexture, 0, Map.Read, 0, &mapped),
            "ID3D11DeviceContext::Map(READ)");

        var pixels = new byte[width * height * 4];
        try
        {
            var source = (byte*)mapped.PData;
            var destinationStride = (int)width * 4;

            // D3D11 pads staging rows to a driver-chosen alignment, so RowPitch is generally wider than
            // width * 4 — copy row by row instead of one flat block.
            for (var y = 0; y < height; y++)
            {
                new ReadOnlySpan<byte>(source + (uint)y * mapped.RowPitch, destinationStride)
                    .CopyTo(pixels.AsSpan(y * destinationStride, destinationStride));
            }
        }
        finally
        {
            _context->Unmap((ID3D11Resource*)_stagingTexture, 0);
        }

        // Unlike OpenGL, D3D's texture space already has its origin at the top-left, so the row flip the
        // GL backend needs would corrupt the image here.

        if (format == TextureFormat.Bgra8UNorm)
            SwapRedAndBlue(pixels);

        return pixels;
    }

    private void EnsureStagingTexture(uint width, uint height, TextureFormat format)
    {
        if (_stagingTexture is not null && _stagingWidth == width && _stagingHeight == height && _stagingFormat == format)
            return;

        if (_stagingTexture is not null)
        {
            _stagingTexture->Release();
            _stagingTexture = null;
        }

        var desc = new Texture2DDesc
        {
            Width = width,
            Height = height,
            MipLevels = 1,
            ArraySize = 1,
            Format = D3D11Formats.MapConcrete(format),
            SampleDesc = new SampleDesc(1, 0),
            Usage = Usage.Staging,
            BindFlags = 0,
            CPUAccessFlags = (uint)CpuAccessFlag.Read,
            MiscFlags = 0,
        };

        ID3D11Texture2D* staging = null;
        Check(_device->CreateTexture2D(&desc, null, ref staging), "ID3D11Device::CreateTexture2D(staging)");

        _stagingTexture = staging;
        _stagingWidth = width;
        _stagingHeight = height;
        _stagingFormat = format;
    }

    /// <summary>The abstraction always hands back RGBA8; a BGRA target's bytes need their channels swapped.</summary>
    private static void SwapRedAndBlue(byte[] pixels)
    {
        for (var i = 0; i + 4 <= pixels.Length; i += 4)
            (pixels[i], pixels[i + 2]) = (pixels[i + 2], pixels[i]);
    }

    public void Dispose()
    {
        if (_stagingTexture is not null) { _stagingTexture->Release(); _stagingTexture = null; }

        if (_context is not null)
        {
            _context->ClearState();
            _context->Flush();
            _context->Release();
            _context = null;
        }

        if (_device is not null) { _device->Release(); _device = null; }

        _d3d11?.Dispose();
        _d3d11 = null!;
    }
}
