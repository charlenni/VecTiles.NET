using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Quadtree;
using SkiaSharp;
using VecTiles.Common.Enums;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Renderers.Skia.Extensions;
using VecTiles.Styles.OpenMapTiles.Extensions;

namespace VecTiles.Renderers.Skia;

public static class IconLineSymbolRenderer
{
    private static readonly SKSamplingOptions SamplingOptions = new SKSamplingOptions(new SKCubicResampler(0.5f, 0.5f));
    private static readonly SKPaint DebugPaint = new SKPaint { Color = SKColors.Aqua, StrokeWidth = 1, IsStroke = true };

    public static void DrawIcon(SKCanvas canvas, EvaluationContext context, IconLineSymbol symbol, double screenX,
        double screenY, double rotation, bool showValidBorders = false)
    {
        SKPaint paint = new SKPaint() {IsAntialias = true};

        canvas.Save();

        canvas.Translate((float) screenX, (float) screenY);

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
        canvas.RotateDegrees((float) rotation);

        canvas.Translate((float) (symbol.Anchor.X * symbol.Icon.Width * symbol.Scale - symbol.Padding),
            (float) (symbol.Anchor.Y * symbol.Icon.Height * symbol.Scale - symbol.Padding));

        symbol.Icon.Atlas.Native ??= SKImage.FromEncodedData(symbol.Icon.Atlas.Binary);
        symbol.Icon.Native ??= ((SKImage) symbol.Icon.Atlas.Native).Subset(new SKRectI(symbol.Icon.X, symbol.Icon.Y,
            symbol.Icon.X + symbol.Icon.Width, symbol.Icon.Y + symbol.Icon.Height));

        var dest = new SKRect(symbol.Padding, symbol.Padding, symbol.Icon.Width * symbol.Scale + symbol.Padding,
            symbol.Icon.Height * symbol.Scale + symbol.Padding);

        canvas.DrawImage((SKImage) symbol.Icon.Native, dest, SamplingOptions, paint);

        canvas.Restore();

        if (showValidBorders)
        {
            canvas.DrawRect(
                new SKRect((float) symbol.Envelope!.MinX, (float) symbol.Envelope!.MinY, (float) symbol.Envelope!.MaxX,
                    (float) symbol.Envelope!.MaxY), DebugPaint);
        }
    }

    private static Envelope CreateEnvelope(SKCanvas canvas, EvaluationContext context, IconLineSymbol symbol, double screenX, double screenY)
    {
        if (symbol.Icon == null)
        {
            return new Envelope();
        }

        var width = symbol.Icon.Width * symbol.Scale + symbol.Padding * 2;
        var height = symbol.Icon.Height * symbol.Scale + symbol.Padding * 2;
        var anchor = new Point(symbol.Anchor.X * width, symbol.Anchor.Y * height);
        var offset = new Point(anchor.X + symbol.Offset.X, anchor.Y + symbol.Offset.Y);

        // We now could calc the rough envelope of icon
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

    public static bool CheckForSingleSpace(SKCanvas canvas, EvaluationContext context, IconLineSymbol symbol, Quadtree<ISymbol> tree, double screenX, double screenY, double rotation, bool showUnvalidBorders)
    {

        symbol.Envelope = CreateEnvelope(canvas, context, symbol, screenX, screenY);

        if (symbol.Envelope.MaxX < canvas.LocalClipBounds.Left ||
            symbol.Envelope.MinY < canvas.LocalClipBounds.Top ||
            symbol.Envelope.MinX > canvas.LocalClipBounds.Left + canvas.LocalClipBounds.Width ||
            symbol.Envelope.MinY > canvas.LocalClipBounds.Top + canvas.LocalClipBounds.Height)
        {
            // Symbol isn't visible
            return false;
        }

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

            if (symbol.AllowOthers && otherSymbol.AllowOthers)
            {
                continue;
            }
            
            if (showUnvalidBorders && symbol.Name != otherSymbol.Name)
            {
                canvas.DrawRect(new SKRect((float)symbol.Envelope.MinX, (float)symbol.Envelope.MaxY, (float)symbol.Envelope.MaxX, (float)symbol.Envelope.MinY), DebugPaint);
            }

            return false;
        }

        return true;
    }
}