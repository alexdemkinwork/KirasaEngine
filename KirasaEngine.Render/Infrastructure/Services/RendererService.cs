namespace KirasaEngine.Render.Infrastructure.Services;
[RegisterScoped]
public class RendererService(TimeService timeService) : IDisposable
{
    public TypeBackendRender SelectedBackendRender { get; set; }
    private Dictionary<TypeBackendRender ,IRendererBase> _renderBackends { get; set; }
    public IRendererBase SelectedBackendRenderer => _renderBackends[SelectedBackendRender];
    public bool IsInitialized { get; private set; } = false;
    public void Initialize(RenderScene scene)
    {
        _renderBackends = new();
        _renderBackends!.Add(TypeBackendRender.Raylib, new RaylibRender(scene));
        SelectedBackendRender = scene.TypeBackend;
        IsInitialized = true;
    }

    private void Present(List<LayerScene> layers)
    {
        SelectedBackendRenderer.ClearBackground();
        foreach (var layer in layers) SelectedBackendRenderer.Render(layer.Nodes);
        if (SelectedBackendRenderer.ShowFrame) SelectedBackendRenderer.ShowFramerate(timeService.GetDeltaTimeSeconds());
    }
    public void RunTexture(List<LayerScene> layers, Action<byte[]>? onFrameUpdated, CancellationToken cancellationToken = default)
    {
        while (SelectedBackendRenderer.SurfaceIsShowed() && !cancellationToken.IsCancellationRequested)
        {
            timeService.ReadTime();
            SelectedBackendRenderer.BeginRenderTexture();
            Present(layers);
            
            SelectedBackendRenderer.EndRenderTexture();
            var textureData = GetAdditionalRendererFunctionality<RaylibRender>(TypeBackendRender.Raylib)
                .GetRenderTextureData();
            onFrameUpdated?.Invoke(textureData);
            timeService.UpdateDeltaTime();
        }
        Dispose();
    }
    
    public void RunSurface(List<LayerScene> layers)
    {
        while (SelectedBackendRenderer.SurfaceIsShowed())
        {
            timeService.ReadTime();
            SelectedBackendRenderer.BeginRenderSurface();
            Present(layers);
            SelectedBackendRenderer.EndRenderSurface();
            timeService.UpdateDeltaTime();
        }
        Dispose();
    }
    private T GetAdditionalRendererFunctionality<T>(TypeBackendRender typeBackendRender) => (T)_renderBackends[typeBackendRender];

    public void Dispose()
    {
        foreach (IRendererBase renderer in _renderBackends.Values) renderer.Terminate();
    }
}