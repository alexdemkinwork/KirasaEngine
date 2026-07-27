namespace KirasaEngine.Editor.Views.Windows;
// Временные конкретные классы для теста
public partial class MainWindow : Window
{
    private MainViewModel ViewModel => (DataContext as MainViewModel)!;
    private RenderFrame? _renderFrame;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Render = new(ViewModel.RenderFrameViewModel);
    }

    private void Button_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        RenderScene scene = new()
        {
            Title = string.Empty,
            BackgroundColor = new() { Hex = "#000000" },
            TypeBackend = TypeBackendRender.Raylib,
            ShowFrame = true,
            HeightResolution = 720,
            WidthResolution = 1080,
            RenderTexture = true,
            Layers = new()
        };
        
        Render.ViewModel.UpdateScene(scene);
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        ViewModel.RenderFrameViewModel.Dispose();
        Render.ViewModel.Dispose();
    }
}