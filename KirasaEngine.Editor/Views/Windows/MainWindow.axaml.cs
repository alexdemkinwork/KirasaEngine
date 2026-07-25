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
        //Frame.Bind(Image.SourceProperty, new Binding(nameof(ViewModel.RenderFrameViewModel._sourceFrame)));
    }

    private void Button_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_renderFrame != null)
        {
            // Отменяем предыдущий рендеринг
            ViewModel.RenderFrameViewModel.CancelRendering();
            
            _renderFrame = null;
        }

        _renderFrame = new RenderFrame(ViewModel.RenderFrameViewModel);
       
    }
}