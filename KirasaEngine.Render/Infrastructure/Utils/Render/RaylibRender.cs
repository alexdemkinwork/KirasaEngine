using System.Diagnostics;

namespace KirasaEngine.Render.Infrastructure.Utils.Render;
[RegisterScoped]
public class RaylibRender : IRendererBase<RenderTexture2D, Color>
{
    public float counter;
    public int WidthRender { get; set; }
    public int HeightRender { get; set; }
    public Color BackgroundColor { get; set; }
    public bool ShowFrame { get; set; }
    public RenderTexture2D Texture { get; set; }
    public RaylibRender(RenderScene scene)
    {
        ShowFrame = scene.ShowFrame;
        WidthRender = scene.WidthResolution;
        HeightRender = scene.HeightResolution;
        BackgroundColor = ParseColorToBackendColor(scene.BackgroundColor);
        if (scene.RenderTexture) Raylib.SetConfigFlags(ConfigFlags.HiddenWindow);
        Raylib.InitWindow(WidthRender, HeightRender, scene.Title);
        Texture = Raylib.LoadRenderTexture(WidthRender, HeightRender);
    }

    public void ShowFramerate(float dt)
    {
        Raylib.DrawText($"{(int)(1 / dt)} FPS", 0, 0, 30, Color.Yellow);
    }

    public void ClearBackground() => Raylib.ClearBackground(BackgroundColor);

    public void Render(List<RenderNode> nodes)
    {
        foreach (RenderNode node in nodes)
        {
            if (node is RenderNodeModificator<LineModificator> nodeLine) DrawLine(nodeLine);
            else if (node is RenderNodeModificator<LinesModificator> nodeLines) DrawLines(nodeLines);
            else if (node is RenderNodeModificator<TextureModificator<RenderTexture2D>> nodeTexture) DrawTexture(nodeTexture);
        }
    }

    public void BeginRenderSurface() => Raylib.BeginDrawing();
    public void EndRenderSurface() => Raylib.EndDrawing();
    public bool CanBeginRenderTexture() => Raylib.IsRenderTextureValid(Texture);

    public void BeginRenderTexture() => Raylib.BeginTextureMode(Texture);
    public void EndRenderTexture() => Raylib.EndTextureMode();
    #region FiguresDrawable

    public void DrawLine(RenderNodeModificator<LineModificator> node)
    {
        var pos1 = node.Position + node.Modificator.Point1;
        var pos2 = node.Position + node.Modificator.Point2;
        
        switch (node.Modificator.LineType)
        {
            case LineType.Default:
                if (node.StrokeScale >= 0.0f) Raylib.DrawLineEx(pos1, pos2, 
                    node.StrokeScale + node.Modificator.Stroke, 
                    ParseColorToBackendColor(node.StrokeColor));
                Raylib.DrawLineEx(pos1, pos2,  
                    node.Modificator.Stroke, 
                    ParseColorToBackendColor(node.BackgroundColor));
                break;
            case LineType.Bezier:
                if (node.StrokeScale >= 0.0f) Raylib.DrawLineBezier(pos1, pos2,
                    (float)node.Modificator.BezierLength! + node.StrokeScale,
                    ParseColorToBackendColor(node.StrokeColor)
                );
                Raylib.DrawLineBezier(pos1, pos2,
                    (float)node.Modificator.BezierLength!,
                    ParseColorToBackendColor(node.BackgroundColor));
                break;
            case LineType.Dash:
                if (node.StrokeScale >= 0.0f) Raylib.DrawLineDashed(pos1, pos2, 
                    (int)node.Modificator.DashLength! + (int)node.StrokeScale,
                    (int)node.Modificator.DashSpacing! - (int)node.StrokeScale,
                    ParseColorToBackendColor(node.StrokeColor));
                Raylib.DrawLineDashed(pos1, pos2,
                    (int)node.Modificator.DashLength!,
                    (int)node.Modificator.DashSpacing!,
                    ParseColorToBackendColor(node.BackgroundColor));
                break;
        }
    }

    public void DrawLines(RenderNodeModificator<LinesModificator> node)
    {
        foreach (var line in node.Modificator.Lines)
        {
            line.Position += node.Position;
            DrawLine(line);
        }
    }

    public void UpdateBounds(int x, int y, int width, int height)
    {
        Raylib.SetWindowPosition(x, y);
        Raylib.SetWindowSize(width, height);
    }

    public bool SurfaceIsShowed() => !Raylib.WindowShouldClose();

    public void DrawTexture(RenderNodeModificator<TextureModificator<RenderTexture2D>> node)
    {
        Raylib.DrawTexturePro(
            node.Modificator.Texture.Texture,
            Parser.Vector4ToRectangle(node.Modificator.Source),
            Parser.Vector4ToRectangle(node.Modificator.Destination),
            node.SelfOriginPoint,
            node.Rotation,
            ParseColorToBackendColor(node.BackgroundColor));
    }

    #endregion
    
    public Color ParseColorToBackendColor(RenderColor color)
    {
        var hex = color.Hex.TrimStart('#');

        if (hex.Length == 3)
            hex = string.Concat(hex[0], hex[0], hex[1], hex[1], hex[2], hex[2]);

        if (hex.Length != 6)
            throw new ArgumentException("Hex must be 3 or 6 digits.");

        byte r = Convert.ToByte(hex.Substring(0, 2), 16);
        byte g = Convert.ToByte(hex.Substring(2, 2), 16);
        byte b = Convert.ToByte(hex.Substring(4, 2), 16);
        byte a = (byte)Math.Round(color.Opacity * 255);

        return new (r, g, b, a);
    }

    public unsafe byte[] GetRenderTextureData()
    {
        var texture = Texture.Texture;
        Image img = Raylib.LoadImageFromTexture(texture);
        int size = img.Width * img.Height * 4;
        var data = new byte[size];
        fixed (byte* dst = data)
        {
            Buffer.MemoryCopy(img.Data, dst, size, size);
        }
        Raylib.UnloadImage(img);
        return data;
    }

    public void Terminate()
    {
        Raylib.UnloadRenderTexture(Texture);
        Raylib.CloseWindow();
    }
}