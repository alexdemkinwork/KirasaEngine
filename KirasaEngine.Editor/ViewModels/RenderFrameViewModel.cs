using System.Diagnostics;
using System.Linq;
using System.Threading;
using KirasaEngine.Render.Domain.Models.Render.Scene;
using KirasaEngine.Render.Infrastructure.Services;

namespace KirasaEngine.Editor.ViewModels;
[RegisterScoped]
public partial class RenderFrameViewModel : ViewModelBase
{
    public event Action<WriteableBitmap>? ImageUpdated;
    public WriteableBitmap _sourceFrame;
    private readonly CancellationTokenSource _cts = new();
    public WriteableBitmap SourceFrame
    {
        get => _sourceFrame;
        private set
        {
            SetProperty(ref _sourceFrame, value);
            OnPropertyChanged();
        }
    }
    private bool _startedShowed = false;
    public required RenderScene Scene { get; set; }
    private readonly RendererService rendererService;

    private byte[] _data;
    
    public RenderFrameViewModel(RendererService _renderService) => rendererService = _renderService;   
    public void Initialize() => rendererService.Initialize(Scene);
    
    public void StartRenderingFromThread()
    {
        var thread = new Thread(() =>
        {
            Initialize();
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
            ImageUpdated?.Invoke(newBitmap);
        });
    }
    public void CancelRendering()
    {
        _cts.Cancel();
    }
}