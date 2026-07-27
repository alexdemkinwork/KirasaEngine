using KirasaEngine.Render.Infrastructure.Services;

namespace KirasaEngine.Editor.ViewModels;
[RegisterScoped]
public partial class RenderFrameViewModel(RendererService rendererService) : ViewModelBase, IDisposable
{
    private CancellationTokenSource _cts = new();
    public WriteableBitmap SourceFrame
    {
        get => field;
        private set
        {
            SetProperty(ref field, value);
            OnPropertyChanged();
        }
    }
    public required RenderScene Scene { get; set; }

    public void UpdateScene(RenderScene scene)
    {
        Dispose();
        Scene = scene;
        StartRenderingFromThread();
    }
    
    public void StartRenderingFromThread()
    {
        _cts = new();
        var thread = new Thread(() =>
        {
            rendererService.Initialize(Scene);
            rendererService.RunTexture(
                Scene.Layers.Values.ToList(),
                (data) => UpdateFrame(data), _cts.Token);
        }); ;
        thread.Start();
    }

    private void UpdateFrame(byte[] data)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var renderer = rendererService.SelectedBackendRenderer;
            if (renderer == null || data == null || data.Length == 0) return;

            int w = renderer.WidthRender;
            int h = renderer.HeightRender;
            if (w <= 0 || h <= 0) return;
            
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
        SourceFrame?.Dispose();
        Scene = null;
    }
}