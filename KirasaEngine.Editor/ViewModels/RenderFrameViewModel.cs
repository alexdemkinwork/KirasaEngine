using KirasaEngine.Editor.Infrastructure.Services;
using KirasaEngine.MGL.SceneGraph;
using KirasaEngine.MGL.Models;
using System.Runtime.InteropServices;
using System.Threading;

namespace KirasaEngine.Editor.ViewModels;

[RegisterScoped]
public partial class RenderFrameViewModel(RendererService rendererService) : ViewModelBase, IDisposable
{
    private CancellationTokenSource _cts = new();
    public WriteableBitmap? SourceFrame { get; private set; }
    public Scene? Scene { get; set; }
    public RendererService RendererService => rendererService;

    public void UpdateScene(Scene scene)
    {
        Dispose();
        Scene = scene;
        StartRenderingFromThread();
    }
    
    public void StartRenderingFromThread()
    {
        if (Scene == null) return;
        
        _cts = new();
        var thread = new Thread(() =>
        {
            rendererService.Initialize(Scene, 800, 600);
            rendererService.FrameRendered += OnFrameRendered;
            rendererService.StartRendering(_cts.Token);
        });
        thread.Start();
    }

    private void OnFrameRendered(byte[] data)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (data == null || data.Length == 0) return;

            int w = 800;
            int h = 600;
            
            var newBitmap = new WriteableBitmap(
                new PixelSize(w, h),
                new Vector2(96, 96),
                PixelFormat.Rgba8888,
                AlphaFormat.Premul);

            using var fb = newBitmap.Lock();
            Marshal.Copy(data, 0, fb.Address, data.Length);
            SourceFrame?.Dispose();
            SourceFrame = newBitmap;
        });
    }

    public void Dispose()
    {
        _cts.Cancel();
        rendererService.FrameRendered -= OnFrameRendered;
        SourceFrame?.Dispose();
        Scene = null;
    }
}