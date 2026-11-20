using VecTiles.Common.Enums;
using VecTiles.Common.Primitives;

namespace VecTiles.Common.Interfaces;

/// <summary>
/// Interface for a style file independent class, that could produce one or more SKPaint
/// </summary>
public interface IPaint
{
    PaintStyle Style { get; }

    Color Color { get; }

    Color OutlineColor { get; }

    float Opacity { get; }

    bool IsAntialias { get; }

    float StrokeWidth { get; }

    StrokeCap StrokeCap { get; }

    StrokeJoin StrokeJoin { get; }

    float StrokeMiter { get; }

    ISprite? Pattern { get; }

    float[]? DashArray { get; }

    /// <summary>
    /// Radius of a circle in style px (only used by circle layers)
    /// </summary>
    float Radius { get; }

    /// <summary>
    /// Blur amount of a circle (only used by circle layers)
    /// </summary>
    float Blur { get; }

    void Update(EvaluationContext context);

    float GetMaxStrokeWidth(EvaluationContext context);
    
    float GetStrokeWidthFromIntZoom(EvaluationContext context);
}
