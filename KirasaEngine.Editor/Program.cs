namespace KirasaEngine.Editor;

sealed class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    
    public static AppBuilder BuildAvaloniaApp()
    {
        var buildAvaloniaApp = AppBuilder.Configure<App>();
        buildAvaloniaApp.UsePlatformDetect();
        #if DEBUG
        buildAvaloniaApp.WithDeveloperTools();
        #endif
        buildAvaloniaApp.WithInterFont();
        buildAvaloniaApp.LogToTrace();
        return buildAvaloniaApp;
    }
    
}