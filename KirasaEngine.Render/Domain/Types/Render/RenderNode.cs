using KirasaEngine.Render.Domain.Types.Render.Colors;

namespace KirasaEngine.Render.Domain.Types.Render;

public abstract class RenderNode : IComparable<RenderNode>
{
    private RenderColor _backgroundColor;
    private RenderColor _strokeColor;
    private float _strokeScale;

    /// <summary>
    /// Цвет заднего фона
    /// </summary>
    public RenderColor BackgroundColor
    {
        get => _backgroundColor;
        set
        {
            _backgroundColor = value;
            UpdateRenderContext();
        }
    }

    /// <summary>
    /// Цвет границы
    /// </summary>
    public RenderColor StrokeColor
    {
        get => _strokeColor;
        set
        {
            _strokeColor = value;
            UpdateRenderContext();
        }
    }

    /// <summary>
    /// Размер границы
    /// </summary>
    public float StrokeScale
    {
        get => _strokeScale;
        set
        {
            _strokeScale = value;
            UpdateRenderContext();
        }
    }

    /// <summary>
    /// Идентификатор
    /// </summary>
    public Guid IdNode { get; set; }
    
    /// <summary>
    /// Дочерние графические узлы
    /// </summary>
    public List<RenderNode>? ChildNodes { get; set; }

    /// <summary>
    /// Позиция рендера
    /// </summary>
    public Vector2 Position { get; set; }

    /// <summary>
    /// Точка центра
    /// </summary>
    public Vector2 SelfOriginPoint { get; set; }

    /// <summary>
    /// Точка цели (для орбитального вращения)
    /// </summary>
    public Vector2 TargetPoint { get; set; }

    /// <summary>
    /// Поворот
    /// </summary>
    public float Rotation { get; set; }

    /// <summary>
    /// Номер слоя
    /// </summary>
    public int ZIndex { get; set; } = 0;

    public Vector2 AbsoluteOriginPoint => Position + SelfOriginPoint;

    public RenderNode()
    {
        IdNode = Guid.NewGuid();
        _backgroundColor = new RenderColor("#000000") { Opacity = 0.0f };
        _strokeColor = new RenderColor("#000000") { Opacity = 0.0f };
        _strokeScale = 0.0f;
    }

    public abstract void Draw();
    public abstract void UpdateRenderContext();
    public int CompareTo(RenderNode? other) => IdNode.CompareTo(other?.ZIndex);
}

public abstract class RenderNodeModificator<TModificator> : RenderNode
    where TModificator : BaseModificator<RenderNodeModificator<TModificator>>
{
    /// <summary>
    /// Модификатор рендерного узла
    /// </summary>
    public TModificator Modificator { get; set; }
}