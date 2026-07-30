using KirasaEngine.Editor.Infrastructure.Services;
using KirasaEngine.Editor.ViewModels;
using KirasaEngine.MGL.SceneGraph;

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
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        this.Loaded += RenderFrame_Loaded;
        
    }

    private void RenderFrame_Loaded(object? sender, RoutedEventArgs e) => UpdateResolutionRender((int)Bounds.Width, (int)Bounds.Height);

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e) => FrameContent.InvalidateVisual();
    
    private void FrameContent_OnSizeChanged(object? sender, SizeChangedEventArgs e) => UpdateResolutionRender((int)e.NewSize.Width, (int)e.NewSize.Height);

    private void UpdateResolutionRender(int width, int height)
    {
        var vm = DataContext as RenderFrameViewModel;
        if (vm?.Scene != null)
        {
            vm.Scene.Resize((uint)width, (uint)height);
        }
        // Note: The renderer service handles resizing internally
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ViewModel?.Dispose();
    }
}