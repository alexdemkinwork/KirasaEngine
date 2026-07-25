

namespace KirasaEngine.Editor.Views;

public partial class RenderFrame : UserControl
{
    public RenderFrameViewModel ViewModel => (DataContext as RenderFrameViewModel)!;
    public RenderFrame()
    {
        InitializeComponent();
    }

    public RenderFrame(RenderFrameViewModel renderFrameViewModel) : this()
    {
        DataContext = renderFrameViewModel;
        Width = ViewModel.Scene.WidthResolution;
        Height = ViewModel.Scene.HeightResolution;
        renderFrameViewModel.ImageUpdated += OnImageUpdated;
        ViewModel.StartRenderingFromThread();
    }
    private void OnImageUpdated(WriteableBitmap bitmap)
    {
        // Обновляем Source напрямую
        FrameContent.Source = bitmap;
        // Принудительно перерисовываем (опционально)
        FrameContent.InvalidateVisual();
    }

    private void FrameContent_OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        var vm = DataContext as RenderFrameViewModel;
        vm?.Scene.WidthResolution = (int)e.NewSize.Width;
        vm?.Scene.HeightResolution = (int)e.NewSize.Height;
    }
    
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ViewModel?.ImageUpdated -= OnImageUpdated;
    }
}