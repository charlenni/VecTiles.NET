using SkiaSharp;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;

namespace VecTiles.Renderers.Skia.Extensions;

/// <summary>
/// Extension methods to convert a paint abstraction into SkiaSharp paints.
/// </summary>
public static class IPaintExtension
{
    /// <summary>
    /// Updates the paint for the evaluation context and converts it to one or
    /// more ready to use <see cref="SKPaint" /> instances. A paint combining
    /// fill and stroke is converted to a fill paint followed by an outline
    /// stroke paint.
    /// </summary>
    /// <param name="paint">Paint to convert; its values are updated for the context.</param>
    /// <param name="context">Context used to evaluate data-driven paint properties.</param>
    /// <returns>The SkiaSharp paints to draw with.</returns>
    public static IEnumerable<SKPaint> ToSKPaint(this IPaint paint, EvaluationContext context)
    {
        paint.Update(context);

        var skPaint = new SKPaint {
            Style = (SKPaintStyle)paint.Style,
            Color = paint.Color.ToSKColor(),
            BlendMode = SKBlendMode.SrcOver,
            IsAntialias = paint.IsAntialias,
            StrokeWidth = paint.StrokeWidth,
            StrokeCap = (SKStrokeCap)paint.StrokeCap,
            StrokeJoin = (SKStrokeJoin)paint.StrokeJoin,
            StrokeMiter = paint.StrokeMiter,
        };

        if (paint.Pattern != null)
        {
            paint.Pattern.Atlas.Native ??= SKImage.FromEncodedData(paint.Pattern.Atlas.Binary);
            paint.Pattern.Native ??= ((SKImage) paint.Pattern.Atlas.Native).Subset(new SKRectI(paint.Pattern.X, paint.Pattern.Y,
                paint.Pattern.X + paint.Pattern.Width, paint.Pattern.Y + paint.Pattern.Height));
            
            skPaint.Shader = ((SKImage)paint.Pattern.Native).ToShader(SKShaderTileMode.Repeat, SKShaderTileMode.Repeat, SKMatrix.CreateScale(context.Scale, context.Scale));
        }
        
        if (skPaint.Style != SKPaintStyle.StrokeAndFill)
        {
            return new List<SKPaint> { skPaint };
        }

        skPaint.Style = SKPaintStyle.Fill;

        var skLine = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = paint.OutlineColor.ToSKColor(),
            BlendMode = SKBlendMode.SrcOver,
            IsAntialias = paint.IsAntialias,
            StrokeWidth = skPaint.StrokeWidth,
            StrokeCap = skPaint.StrokeCap,
            StrokeJoin = skPaint.StrokeJoin,
            StrokeMiter = skPaint.StrokeMiter,
        };
        
        return new List<SKPaint> { skPaint, skLine };
    }
}
