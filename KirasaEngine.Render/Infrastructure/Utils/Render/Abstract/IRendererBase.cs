namespace KirasaEngine.Render.Infrastructure.Utils.Render.Abstract;

public interface IRendererBase
{
    public int WidthRender { get; set; }
    public int HeightRender { get; set; }
    public Color BackgroundColor { get; set; }
    public bool ShowFrame { get; set; }

    public void ShowFramerate(float dt);
    
    #region BaseRenderer
    public void ClearBackground();
    public void Render(List<RenderNode> nodes);
    public void BeginRenderSurface();
    public void EndRenderSurface();
    public bool CanBeginRenderTexture();
    public void BeginRenderTexture();
    public void EndRenderTexture();
    #endregion
    
    #region  DrawableFigures
    public void DrawLine(RenderNodeModificator<LineModificator> node);
    public void DrawLines(RenderNodeModificator<LinesModificator> node);
    
    #endregion

    public void UpdateBounds(int x, int y, int width, int height);
    public bool SurfaceIsShowed();
    public byte[] GetRenderTextureData();

    public void Terminate();
}

public interface IRendererBase<TRenderTexture, TColor> : IRendererBase
{
    public TRenderTexture Texture { get; set; }
    public void DrawTexture(RenderNodeModificator<TextureModificator<TRenderTexture>> node);
    public TColor ParseColorToBackendColor(RenderColor color);
}