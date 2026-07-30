using KirasaEngine.MGL.Smoke;

const uint width = 512;
const uint height = 384;

var backendsArg = args.Length > 0 ? args[0] : null;
var backends = backendsArg switch
{
    null => [GraphicsBackend.Direct3D12],
    _ => backendsArg.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(s => Enum.Parse<GraphicsBackend>(s, ignoreCase: true)).ToArray(),
};

var outDir = Path.Combine(AppContext.BaseDirectory, "smoke-output");
Directory.CreateDirectory(outDir);

var failures = new List<string>();

foreach (var backend in backends)
{
    Console.WriteLine($"=== {backend} ===");
    try
    {
        RunBackend(backend, outDir);
        Console.WriteLine($"{backend}: OK");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"{backend}: FAILED - {ex}");
        failures.Add(backend.ToString());
    }
}

if (failures.Count > 0)
{
    Console.WriteLine($"FAILED backends: {string.Join(", ", failures)}");
    return 1;
}

Console.WriteLine("All backends OK.");
return 0;

static void RunBackend(GraphicsBackend backend, string outDir)
{
    var window = GraphicsDeviceFactory.CreateWindow(backend, width, height, $"MGL Smoke [{backend}]", visible: false);
    window.Initialize();

    var device = GraphicsDeviceFactory.Create(backend);
    device.Initialize(new GraphicsDeviceDescription { Window = window, Width = width, Height = height });

    var renderer = new SceneRenderer(device);
    try
    {
        var scene = DemoScene.Build();

        var pixels = renderer.RenderToTexture(scene, width, height);

        var expectedLength = (int)(width * height * 4);
        if (pixels.Length != expectedLength)
            throw new InvalidOperationException($"Unexpected buffer size {pixels.Length}, expected {expectedLength}.");

        AnalyzePixels(pixels);

        var path = Path.Combine(outDir, $"{backend}.bmp");
        BmpWriter.WriteRgba(path, pixels, (int)width, (int)height);
        Console.WriteLine($"  wrote {path}");
    }
    finally
    {
        // GPU resource cleanup must run while the context is still alive, so dispose in this exact
        // order: renderer/device (both talk to the GPU context) before the window that owns that context.
        renderer.Dispose();
        device.Dispose();
        window.Dispose();
    }
}

static void AnalyzePixels(byte[] pixels)
{
    var distinct = new HashSet<int>();
    for (var i = 0; i + 4 <= pixels.Length && distinct.Count < 5000; i += 4)
        distinct.Add(pixels[i] | (pixels[i + 1] << 8) | (pixels[i + 2] << 16));

    Console.WriteLine($"  distinct colors (capped at 5000): {distinct.Count}");
    if (distinct.Count <= 1)
        throw new InvalidOperationException("Rendered image is a single flat color - nothing was drawn.");
}
