namespace KirasaEngine.Editor.ViewModels;
[RegisterTransient]
public partial class MainViewModel(RenderFrameViewModel renderFrameViewModel) : ViewModelBase
{
    public RenderFrameViewModel RenderFrameViewModel => renderFrameViewModel;
    [ObservableProperty] public partial string Greeting { get; set; } = "Запустить!";
}