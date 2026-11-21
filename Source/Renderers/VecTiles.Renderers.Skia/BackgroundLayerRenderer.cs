using SkiaSharp;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Renderers.Common.Interfaces;
using VecTiles.Renderers.Skia.Extensions;

namespace VecTiles.Renderers.Skia;

/// <summary>
/// Draws the background of a tile: fills the whole tile rectangle with the
/// background paint of the style layer.
/// </summary>
public class BackgroundLayerRenderer : ILayerRenderer
{
    readonly SKRect _tileRect;
    readonly IPaint _paint;

    /// <summary>
    /// Create a background layer renderer for the given tile rectangle and paint.
    /// </summary>
    /// <param name="rect">Rectangle of the tile in canvas coordinates.</param>
    /// <param name="paint">Background paint evaluated at draw time.</param>
    public BackgroundLayerRenderer(SKRect rect, IPaint paint)
    {
        _tileRect = rect;
        _paint = paint;
    }

    /// <inheritdoc />
    public void Draw(object canvas, EvaluationContext context)
    {
        if (canvas is not SKCanvas skCanvas)
        {
            return;
        }

        foreach (var skPaint in _paint.ToSKPaint(context))
        {
            skCanvas.DrawRect(_tileRect, skPaint);
        }
    }
}
