using KirasaEngine.MGL.Rendering;
using KirasaEngine.MGL.Rendering.Abstractions.Descriptions;
using KirasaEngine.MGL.Rendering.Abstractions.Enums;
using KirasaEngine.MGL.Smoke;

var outDir = Path.Combine(AppContext.BaseDirectory, "diag-output");
Directory.CreateDirectory(outDir);

uint width = 512;
uint height = 384;

void BackendTest(GraphicsBackend backend, string label)
{
    var window = GraphicsDeviceFactory.CreateWindow(backend, width, height, label, visible: false);
    window.Initialize();
    var device = GraphicsDeviceFactory.Create(backend);
    device.Initialize(new GraphicsDeviceDescription { Window = window, Width = width, Height = height });

    var scene = DemoScene.Build();

    // Shadow ON, SSAO OFF
    var rendererOn = new SceneRenderer(device, new PostProcessSettings { ShadowQuality = RenderQuality.High, SSAOQuality = RenderQuality.Off });
    var pixelsOn = rendererOn.RenderToTexture(scene, width, height);
    BmpWriter.WriteRgba(Path.Combine(outDir, $"{label}_shadow_on.bmp"), pixelsOn, (int)width, (int)height);
    rendererOn.Dispose();

    // Shadow OFF, SSAO OFF
    var rendererOff = new SceneRenderer(device, new PostProcessSettings { ShadowQuality = RenderQuality.Off, SSAOQuality = RenderQuality.Off });
    var pixelsOff = rendererOff.RenderToTexture(scene, width, height);
    BmpWriter.WriteRgba(Path.Combine(outDir, $"{label}_shadow_off.bmp"), pixelsOff, (int)width, (int)height);
    rendererOff.Dispose();

    // Shadow ON, SSAO ON
    var rendererAll = new SceneRenderer(device, new PostProcessSettings { ShadowQuality = RenderQuality.High, SSAOQuality = RenderQuality.High });
    var pixelsAll = rendererAll.RenderToTexture(scene, width, height);
    BmpWriter.WriteRgba(Path.Combine(outDir, $"{label}_all_on.bmp"), pixelsAll, (int)width, (int)height);
    rendererAll.Dispose();

    device.Dispose();
    window.Dispose();

    // Analyze shadow-only (on vs off)
    int shadowPixels = 0;
    for (int i = 0; i < pixelsOn.Length; i += 4)
    {
        double brDiff = (Math.Abs(pixelsOn[i] - pixelsOff[i]) + Math.Abs(pixelsOn[i + 1] - pixelsOff[i + 1]) + Math.Abs(pixelsOn[i + 2] - pixelsOff[i + 2])) / 3.0;
        if (brDiff > 2) shadowPixels++;
    }
    int total = pixelsOn.Length / 4;
    Console.WriteLine($"{label}: shadow={shadowPixels}/{total} ({shadowPixels * 100.0 / total:F1}%)");
}

Console.WriteLine("=== Shadow diagnostic ===");
BackendTest(GraphicsBackend.Direct3D11, "D3D11");
BackendTest(GraphicsBackend.OpenGL, "OpenGL");
BackendTest(GraphicsBackend.Vulkan, "Vulkan");
