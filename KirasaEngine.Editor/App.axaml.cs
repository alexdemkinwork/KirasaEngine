namespace KirasaEngine.Editor;

using KirasaEngine.Editor.Views.Windows;

public partial class App : Application
{
    public IServiceProvider ServiceProvider { get; private set; }
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        RegisterServices();

        CultureManager.SetCulture(CultureType.en_US);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow(GetService<MainViewModel>());
        }
        
        base.OnFrameworkInitializationCompleted();
    }

    public TService GetService<TService>() where TService : class => ServiceProvider.GetRequiredService<TService>();

    public override void RegisterServices()
    {
        base.RegisterServices();
        var services = new ServiceCollection();
        services.AddKirasaEngineCore();
        services.AddKirasaEngineBulding();
        services.AddKirasaEngineMGL();
        services.AddKirasaEngineEditor();
        ServiceProvider = services.BuildServiceProvider();
    }
}