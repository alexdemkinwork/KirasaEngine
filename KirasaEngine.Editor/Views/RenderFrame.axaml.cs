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

    private void RenderFrame_Loaded(object? sender, RoutedEventArgs e) => UpdateResoulutionRender((int)Bounds.Width, (int)Bounds.Height);

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e) => FrameContent.InvalidateVisual();
    
    private void FrameContent_OnSizeChanged(object? sender, SizeChangedEventArgs e) => UpdateResoulutionRender((int)e.NewSize.Width, (int)e.NewSize.Height);

    private void UpdateResoulutionRender(int width, int height)
    {
        var vm = DataContext as RenderFrameViewModel;
        vm?.Scene?.WidthResolution = width;
        vm?.Scene?.HeightResolution = height;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ViewModel?.Dispose();
    }
}