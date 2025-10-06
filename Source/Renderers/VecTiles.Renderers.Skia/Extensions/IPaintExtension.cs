using SkiaSharp;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;

namespace VecTiles.Renderers.Skia.Extensions;

public static class IPaintExtension
{
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
            if (paint.Pattern.Native == null)
            {
                paint.Pattern.Native = SKImage.FromEncodedData(paint.Pattern.Binary).Subset(new SKRectI(paint.Pattern.X, paint.Pattern.Y, paint.Pattern.X + paint.Pattern.Width, paint.Pattern.Y + paint.Pattern.Height));
            }
            skPaint.Shader = ((SKImage)paint.Pattern.Native).ToShader(SKShaderTileMode.Repeat, SKShaderTileMode.Repeat, SKMatrix.CreateScale(context.Scale, context.Scale));
        }
        
        if (paint.DashArray != null)
        {
            skPaint.PathEffect = SKPathEffect.CreateDash(paint.DashArray, 0);

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
            IsAntialias = skPaint.IsAntialias,
            StrokeWidth = skPaint.StrokeWidth,
            StrokeCap = skPaint.StrokeCap,
            StrokeJoin = skPaint.StrokeJoin,
            StrokeMiter = skPaint.StrokeMiter,
        };
        
        return new List<SKPaint> { skPaint, skLine };
    }
}
