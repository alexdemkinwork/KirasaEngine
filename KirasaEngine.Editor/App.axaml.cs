namespace KirasaEngine.Editor;

public partial class App : Application
{
    public IServiceProvider ServiceProvider { get; private set; }
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        services.AddKirasaEngineCore();
        services.AddKirasaEngineBulding();
        services.AddKirasaEngineRender();
        services.AddKirasaEngineEditor();
        ServiceProvider = services.BuildServiceProvider();
        
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow(ServiceProvider.GetRequiredService<MainViewModel>());
        }

        base.OnFrameworkInitializationCompleted();
    }
}