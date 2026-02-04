using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Quadtree;
using SkiaSharp;
using VecTiles.Common.Enums;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Renderers.Common.Interfaces;
using VecTiles.Renderers.Skia.Extensions;
using VecTiles.Styles.OpenMapTiles.Extensions;

namespace VecTiles.Renderers.Skia;

public class IconPointSymbolRenderer : ISymbolRenderer
{
    private static readonly SKSamplingOptions _samplingOptions = new SKSamplingOptions(new SKCubicResampler(0.5f, 0.5f));
    private static readonly SKPaint DebugPaint = new SKPaint { Color = SKColors.Green, StrokeWidth = 1, IsStroke = true };

    public static bool CheckForSpace(SKCanvas canvas, EvaluationContext context, ISymbol sym, Quadtree<ISymbol> tree, Func<double, double, (double, double)> worldToScreenConverter, bool showUnvalidBorders = false)
    {
        if (sym is not IconPointSymbol symbol)
        {
            return false;
        }
        
        var (screenX, screenY) = worldToScreenConverter(symbol.Point.X, symbol.Point.Y);

        symbol.Envelope = CreateEnvelope(canvas, context, symbol, screenX, screenY);

        var symbols = tree.Query(symbol.Envelope);

        foreach (var other in symbols)
        {
            if (other is not Symbol otherSymbol)
            {
                continue;
            }
            
            if (otherSymbol.Envelope == null)
            {
                // Should not happen
                continue;
            }

            if (!symbol.Envelope.Intersects(otherSymbol.Envelope))
            {
                continue;
            }

            if (!otherSymbol.AllowOthers)
            {
                if (showUnvalidBorders && sym.Name != symbol.Name)
                {
                    canvas.DrawRect(new SKRect((float)symbol.Envelope!.MinX, (float)symbol.Envelope!.MinY, (float)symbol.Envelope!.MaxX, (float)symbol.Envelope!.MaxY), DebugPaint);
                }

                return false;
            }
        }

        return true;
    }

    private static Envelope CreateEnvelope(SKCanvas canvas, EvaluationContext context, IconPointSymbol symbol, double screenX, double screenY)
    {
        if (symbol.Icon == null)
        {
            return new Envelope();
        }

        var width = symbol.Icon.Width * symbol.Scale + symbol.Padding * 2;
        var height = symbol.Icon.Height * symbol.Scale + symbol.Padding * 2;
        var anchor = new Point(symbol.Anchor.X * width, symbol.Anchor.Y * height);
        var offset = new Point(anchor.X + symbol.Offset.X, anchor.Y + symbol.Offset.Y);

        // We now could calc the rughly envelope of icon
        var envelope = new Envelope(0 + offset.X, width + offset.X, 0 + offset.Y, height + offset.Y);

        if (symbol.Rotation != 0.0)
        {
            envelope.RotateDegrees(symbol.Rotation);
        }

        envelope.Translate(screenX, screenY);

        if (symbol.Translate != null)
        {
            var translate = symbol.Translate?.Invoke(context) ?? new Point(0, 0);
            var translateAnchor = symbol.TranslateAnchor?.Invoke(context) ?? MapAlignment.Map;

            if (translateAnchor == MapAlignment.Map)
            {
                var rotation = context.Rotation * Math.PI / 180.0;
                var cos = Math.Cos(rotation);
                var sin = Math.Sin(rotation);
                var x = translate.X * cos - translate.Y * sin;
                var y = translate.X * sin + translate.Y * cos;
                translate = new Point((float)x, (float)y);
            }

            envelope.Translate(translate.X, translate.Y);
        }

        return envelope;
    }

    public static void Draw(SKCanvas canvas, EvaluationContext context, ISymbol sym, ref Quadtree<ISymbol> tree, Func<double, double, (double, double)> worldToScreenConverter, bool showValidBorders = false)
    {
        if (sym is not IconPointSymbol symbol)
        {
            return;
        }
        
        var (screenX, screenY) = worldToScreenConverter(symbol.Point.X, symbol.Point.Y);

        if (symbol.Envelope is null || symbol.Envelope.IsNull)
        {
            return;
        }

        DrawIcon(canvas, context, symbol, screenX, screenY, showValidBorders);

        tree.Insert(symbol.Envelope, symbol);
    }

    private static void DrawIcon(SKCanvas canvas, EvaluationContext context, IconPointSymbol symbol, double screenX, double screenY, bool showValidBorders = false)
    {
        SKPaint paint = new SKPaint() { IsAntialias = true };

        canvas.Save();

        canvas.Translate((float)screenX, (float)screenY);

        if (symbol.ColorFilter != null)
        {
            paint.ColorFilter = SKColorFilter.CreateColorMatrix(symbol.ColorFilter(context));
        }

        if (symbol.Translate != null)
        {
            var translate = symbol.Translate?.Invoke(context) ?? new Point(0, 0);
            var translateAnchor = symbol.TranslateAnchor?.Invoke(context) ?? MapAlignment.Map;

            if (translateAnchor == MapAlignment.Viewport)
            {
                canvas.RotateDegrees(-context.Rotation);
            }

            canvas.Translate(translate.ToSKPoint());
        }

        canvas.Scale(1f / context.Scale);
        canvas.Translate(symbol.Offset.ToSKPoint());
        canvas.RotateDegrees(symbol.Rotation);

        canvas.Translate((float)(symbol.Anchor.X * symbol.Icon.Width * symbol.Scale - symbol.Padding), (float)(symbol.Anchor.Y * symbol.Icon.Height * symbol.Scale - symbol.Padding));

        symbol.Icon.Atlas.Native ??= SKImage.FromEncodedData(symbol.Icon.Atlas.Binary);
        symbol.Icon.Native ??= ((SKImage) symbol.Icon.Atlas.Native).Subset(new SKRectI(symbol.Icon.X, symbol.Icon.Y,
            symbol.Icon.X + symbol.Icon.Width, symbol.Icon.Y + symbol.Icon.Height));

        var dest = new SKRect(symbol.Padding, symbol.Padding, symbol.Icon.Width * symbol.Scale + symbol.Padding, symbol.Icon.Height * symbol.Scale + symbol.Padding);
        
        canvas.DrawImage((SKImage)symbol.Icon.Native, dest, _samplingOptions, paint);

        canvas.Restore();

        if (showValidBorders)
        {
            canvas.DrawRect(
                new SKRect((float) symbol.Envelope!.MinX, (float) symbol.Envelope!.MinY, (float) symbol.Envelope!.MaxX,
                    (float) symbol.Envelope!.MaxY), DebugPaint);
        }
    }
    
    internal static (float rotationDeg, float scaleX, float scaleY, Point translation) AnalyzeCanvasTransform(SKCanvas canvas)
    {
        var m = canvas.TotalMatrix;

        // Rotation berechnen (in Grad)
        float rotationRad = (float)Math.Atan2(m.SkewY, m.ScaleY);
        float rotationDeg = (float)(rotationRad * (180f / Math.PI));

        // Skalierung berechnen (Betrag der Vektoren)
        float scaleX = (float)Math.Sqrt(m.ScaleX * m.ScaleX + m.SkewX * m.SkewX);
        float scaleY = (float)Math.Sqrt(m.SkewY * m.SkewY + m.ScaleY * m.ScaleY);

        // Translation (Position)
        var translation = new Point(m.TransX, m.TransY);

        return (rotationDeg, scaleX, scaleY, translation);
    }
}