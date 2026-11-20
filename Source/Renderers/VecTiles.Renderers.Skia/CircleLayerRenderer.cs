using SkiaSharp;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Renderers.Common.Interfaces;
using VecTiles.Renderers.Skia.Extensions;

namespace VecTiles.Renderers.Skia;

public class CircleLayerRenderer : ILayerRenderer, IDisposable
{
    private readonly SKPoint[] _points;
    private readonly IPaint _paint;
    private readonly SKPaint _skPaint = new() { IsAntialias = true };

    public CircleLayerRenderer(SKPoint[] points, IPaint paint)
    {
        _points = points;
        _paint = paint;
    }

    public void Draw(object canvas, EvaluationContext context)
    {
        if (canvas is not SKCanvas skCanvas)
        {
            return;
        }

        // Update values for paint regarding context
        // Radius was evaluated for the current (fractional) zoom and scaled
        // into tile px, so circles grow with the map like lines and fills
        _paint.Update(context);

        var radius = _paint.Radius;

        if (radius <= 0 || _points.Length == 0)
        {
            return;
        }

        skCanvas.Save();

        // circle-blur: 1 blurs the circle such that only the centerpoint has
        // full opacity, so half of the radius is used as sigma
        SKMaskFilter? blur = null;

        if (_paint.Blur > 0)
        {
            blur = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, _paint.Blur * radius / 2f);
            _skPaint.MaskFilter = blur;
        }

        // Draw the circle fill at the circle radius
        _skPaint.Style = SKPaintStyle.Fill;
        _skPaint.Color = _paint.Color.ToSKColor();

        foreach (var point in _points)
        {
            skCanvas.DrawCircle(point.X, point.Y, radius, _skPaint);
        }

        // Draw the circle stroke centered on the circle radius
        if (_paint.StrokeWidth > 0 && _paint.OutlineColor.A > 0)
        {
            _skPaint.Style = SKPaintStyle.Stroke;
            _skPaint.Color = _paint.OutlineColor.ToSKColor();

            foreach (var point in _points)
            {
                skCanvas.DrawCircle(point.X, point.Y, radius, _skPaint);
            }
        }

        _skPaint.MaskFilter = null;
        blur?.Dispose();

        skCanvas.Restore();
    }

    public void Dispose()
    {
        _skPaint.Dispose();
    }
}
