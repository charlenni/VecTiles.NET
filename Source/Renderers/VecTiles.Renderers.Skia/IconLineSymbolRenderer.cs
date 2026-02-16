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

        if (symbol.ColorFilter != null)
        {
            paint.ColorFilter = SKColorFilter.CreateColorMatrix(symbol.ColorFilter(context));
        }

        canvas.Save();
        
        canvas.Translate((float) screenX, (float) screenY);
        canvas.Scale(1f / context.Scale);

        if (symbol.RotationAlignment == MapAlignment.Map)
        {
            canvas.RotateDegrees((float) rotation);
        }

        canvas.Scale(symbol.Scale);
        canvas.RotateDegrees(symbol.Rotation);

        var x = (float)symbol.Envelope.MinX;
        var y = (float)symbol.Envelope.MinY;

        canvas.DrawImage((SKImage) symbol.Icon.Native, x, y, SamplingOptions, paint);

        canvas.Restore();

        if (showValidBorders)
        {
            canvas.DrawRect(
                new SKRect((float) symbol.ScreenEnvelope!.MinX, (float) symbol.ScreenEnvelope!.MinY, (float) symbol.ScreenEnvelope!.MaxX,
                    (float) symbol.ScreenEnvelope!.MaxY), DebugPaint);
        }
    }

    public static bool CheckForSingleSpace(SKCanvas canvas, EvaluationContext context, SKPath path, double startPos, IconLineSymbol symbol, Quadtree<ISymbol> tree, double screenX, double screenY, double rotation, bool showUnvalidBorders)
    {
        symbol.Envelope ??= CreateEnvelope(symbol, context);

        if (symbol.Envelope is null)
        {
            return false;
        }
        
        // Is there enough space to fit text on a segment
        if (!path.CanFitOnNextLineSegment((float)startPos, (float)(symbol.Envelope.MaxX - symbol.Envelope.MinX)))
        {
            return false;
        }
        
        // Save it for later use
        var screenEnvelope = symbol.Envelope.Copy();
        
        // Move symbol's envelope at the screen position
        if (rotation != 0.0)
        {
            screenEnvelope.RotateDegrees(rotation);
        }

        screenEnvelope.Translate(screenX, screenY);

        // Check, if symbol is visible
        if (screenEnvelope.MaxX < canvas.LocalClipBounds.Left ||
            screenEnvelope.MinY < canvas.LocalClipBounds.Top ||
            screenEnvelope.MinX > canvas.LocalClipBounds.Left + canvas.LocalClipBounds.Width ||
            screenEnvelope.MinY > canvas.LocalClipBounds.Top + canvas.LocalClipBounds.Height)
        {
            // Symbol isn't visible
            return false;
        }
 
        var symbols = tree.Query(screenEnvelope);

        foreach (var other in symbols)
        {
            if (other is not Symbol otherSymbol)
            {
                continue;
            }

            if (otherSymbol.ScreenEnvelope == null)
            {
                // Should not happen
                continue;
            }

            if (!screenEnvelope.Intersects(otherSymbol.ScreenEnvelope))
            {
                continue;
            }

            if (symbol.AllowOthers && otherSymbol.AllowOthers)
            {
                continue;
            }
            
            if (showUnvalidBorders && symbol.Name != otherSymbol.Name)
            {
                canvas.DrawRect(new SKRect((float)screenEnvelope.MinX, (float)screenEnvelope.MinY, (float)screenEnvelope.MaxX, (float)screenEnvelope.MaxY), DebugPaint);
            }

            return false;
        }

        symbol.ScreenEnvelope = screenEnvelope;

        return true;
    }
    
    private static Envelope? CreateEnvelope(IconLineSymbol symbol, EvaluationContext context)
    {
        if (symbol.Icon == null)
        {
            return null;
        }

        // We already calculated for this symbol an envelope, so use this
        if (symbol.Envelope is not null)
        {
            return symbol.Envelope;
        }
        
        symbol.Icon.Atlas.Native ??= SKImage.FromEncodedData(symbol.Icon.Atlas.Binary);
        symbol.Icon.Native ??= ((SKImage) symbol.Icon.Atlas.Native).Subset(new SKRectI(symbol.Icon.X, symbol.Icon.Y,
            symbol.Icon.X + symbol.Icon.Width, symbol.Icon.Y + symbol.Icon.Height));

        var width = symbol.Icon.Width * symbol.Scale;
        var height = symbol.Icon.Height * symbol.Scale;
        var anchor = new Point(symbol.Anchor.X * width, symbol.Anchor.Y * height);
        var offset = new Point(anchor.X + symbol.Offset.X, anchor.Y + symbol.Offset.Y);

        // We now could calc the rough envelope of icon
        var envelope = new Envelope(offset.X - symbol.Padding, offset.X + width + symbol.Padding, offset.Y - symbol.Padding, offset.Y + height + symbol.Padding);

        if (symbol.Rotation != 0.0)
        {
            envelope.RotateDegrees(symbol.Rotation);
        }

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
}