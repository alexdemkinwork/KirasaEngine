using System.Diagnostics;

namespace KirasaEngine.Render.Infrastructure.Services;
[RegisterScoped]
public class RendererService(TimeService timeService) 
{
    public TypeBackendRender SelectedBackendRender { get; set; }
    private Dictionary<TypeBackendRender ,IRendererBase> RenderBackends { get; set; }
    public IRendererBase SelectedBackendRenderer => RenderBackends[SelectedBackendRender];
    public void Initialize(RenderScene scene)
    {
        RenderBackends = new Dictionary<TypeBackendRender, IRendererBase>();
        RenderBackends.Add(TypeBackendRender.Raylib, new RaylibRender(scene));
        SelectedBackendRender = scene.TypeBackend;
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
        SelectedBackendRenderer.Terminate();
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
        SelectedBackendRenderer.Terminate();
    }
    private T GetAdditionalRendererFunctionality<T>(TypeBackendRender typeBackendRender) => (T)RenderBackends[typeBackendRender];
    
}