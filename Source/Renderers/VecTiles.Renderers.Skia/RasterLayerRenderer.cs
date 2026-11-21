using SkiaSharp;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Renderers.Common.Interfaces;
using VecTiles.Renderers.Skia.Extensions;

namespace VecTiles.Renderers.Skia;

/// <summary>
/// Draws a raster tile image onto the tile rectangle of the canvas.
/// </summary>
public class RasterLayerRenderer : ILayerRenderer
{
    readonly SKRect _tileRect;
    readonly IPaint _paint;
    readonly SKBitmap _bitmap;

    /// <summary>
    /// Create a raster layer renderer for the given tile rectangle and bitmap.
    /// The bitmap is copied, so the caller can dispose its instance.
    /// </summary>
    /// <param name="rect">Rectangle of the tile in canvas coordinates.</param>
    /// <param name="paint">Raster paint evaluated at draw time.</param>
    /// <param name="bitmap">Tile image to draw.</param>
    public RasterLayerRenderer(SKRect rect, IPaint paint, SKBitmap bitmap)
    {
        _tileRect = rect;
        _paint = paint;
        _bitmap = bitmap.Copy();
    }

    /// <inheritdoc />
    public void Draw(object canvas, EvaluationContext context)
    {
        if (canvas is not SKCanvas skCanvas)
        {
            return;
        }

        var skPaints = _paint.ToSKPaint(context);

        foreach (var skPaint in skPaints)
        {
            skCanvas.DrawBitmap(_bitmap, _tileRect, skPaint);
        }
    }
}
