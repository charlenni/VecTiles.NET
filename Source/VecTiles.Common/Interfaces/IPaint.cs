using VecTiles.Common.Primitives;
using VecTiles.Common.Enums;

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

    void Update(EvaluationContext context);
}
