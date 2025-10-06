using SkiaSharp;
using VecTiles.Renderers.Skia.Extensions;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Renderers.Common.Interfaces;

namespace VecTiles.Renderers.Skia;

public class BackgroundLayer : IStyledLayer
{
    readonly SKRect _tileRect;
    readonly IPaint _paint;

    public BackgroundLayer(SKRect rect, IPaint paint)
    {
        _tileRect = rect;
        _paint = paint;
    }

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
