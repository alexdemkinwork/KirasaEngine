namespace KirasaEngine.Render.Domain.Models.Render.Modificators;

public class TextureModificator<TTexture> : BaseModificator<RenderNodeModificator<TextureModificator<TTexture>>>
{
    public TTexture Texture { get; set; }

    public Vector4 Source => new(PositionGridItem * ScaleGridItem, ScaleGridItem.X, ScaleGridItem.Y);

    public Vector4 Destination { get; set; }
    public Vector2 ScaleGridItem { get; set; }
    public Vector2 PositionGridItem { get; set; }
    public TextureModificator(RenderNodeModificator<TextureModificator<TTexture>> parentRenderNode) : base(parentRenderNode)
    {
        
    }
}