using SkiaSharp;
using VecTiles.Renderers.Skia.Extensions;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Renderers.Common.Interfaces;

namespace VecTiles.Renderers.Skia;

public class RasterLayerRenderer : ILayerRenderer
{
    readonly SKRect _tileRect;
    readonly IPaint _paint;
    readonly SKBitmap _bitmap;

    public RasterLayerRenderer(SKRect rect, IPaint paint, SKBitmap bitmap)
    {
        _tileRect = rect;
        _paint = paint;
        _bitmap = bitmap.Copy();
    }

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
