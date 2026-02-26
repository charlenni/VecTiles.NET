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
    private static readonly SKPaint _paint = new SKPaint { IsAntialias = true, Color = SKColors.White };
    private static readonly SKSamplingOptions SamplingOptions = new SKSamplingOptions(new SKCubicResampler(0.5f, 0.5f));
    private static readonly SKPaint DebugPaint = new SKPaint { Color = SKColors.Aqua, StrokeWidth = 1, IsStroke = true };

    public static void DrawIcon(SKCanvas canvas, EvaluationContext context, IconLineSymbol symbol, double screenX,
        double screenY, bool showValidBorders = false)
    {
        _paint.Color = SKColors.White.WithAlpha((byte)(symbol.Opacity?.Invoke(context) * 255 ?? 255));

        if (symbol.ColorFilter != null)
        {
            _paint.ColorFilter = SKColorFilter.CreateColorMatrix(symbol.ColorFilter(context));
        }

        var image = (SKImage)symbol.Icon.Native!;
        var imgWidth = image.Width;
        var imgHeight = image.Height;
        var anchorX = (float)symbol.Anchor.X;
        var anchorY = (float)symbol.Anchor.Y;
        var offsetX = (float)symbol.Offset.X;
        var offsetY = (float)symbol.Offset.Y;
        var translate = symbol.Translate?.Invoke(context) ?? new Point(0, 0);
        var transX = (float)translate.X;
        var transY = (float)translate.Y;
        var transAnchor = symbol.TranslateAnchor?.Invoke(context) ?? MapAlignment.Map;

        // TranslateAnchor could only be Map or Viewport
        transAnchor = transAnchor == MapAlignment.Auto ? MapAlignment.Map : transAnchor;

        if (transAnchor == MapAlignment.Map)
        {
            var radRotation = context.Rotation * Math.PI / 180.0;
            var cos = Math.Cos(radRotation);
            var sin = Math.Sin(radRotation);
            var x = transX * cos - transY * sin;
            var y = transX * sin + transY * cos;
            transX = (float)x;
            transY = (float)y;
        }

        canvas.Save();

        canvas.Translate((float)screenX, (float)screenY);
        canvas.Translate(transX, transY);
        canvas.Scale(1f / context.Scale * symbol.Scale);

        var rotation = symbol.Rotation;

        if (symbol.RotationAlignment == MapAlignment.Map)
        {
            rotation += context.Rotation;
        }

        rotation = (rotation + 360) % 360;

        canvas.RotateDegrees(rotation, 0f, 0f);

        if (symbol.KeepUpright && rotation > 90 && rotation < 270)
        {
            canvas.RotateDegrees(180f,
                (0.5f - anchorX) * imgWidth,
                (0.5f - anchorY) * imgHeight);
        }

        canvas.DrawImage(image,
            -anchorX * imgWidth + offsetX,
            -anchorY * imgHeight + offsetY,
            SamplingOptions,
            _paint);

        canvas.Restore();

        if (showValidBorders)
        {
            canvas.DrawRect(
                new SKRect((float)symbol.ScreenEnvelope!.MinX, (float)symbol.ScreenEnvelope!.MinY,
                (float)symbol.ScreenEnvelope!.MaxX, (float)symbol.ScreenEnvelope!.MaxY), DebugPaint);
        }
    }

    public static bool CheckForSingleSpace(SKCanvas canvas, 
        EvaluationContext context, 
        SKPath path, double startPos, 
        IconLineSymbol symbol, 
        Quadtree<ISymbol> tree, 
        double screenX, 
        double screenY, 
        bool showUnvalidBorders)
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
        if (context.Rotation != 0.0)
        {
            screenEnvelope.RotateDegrees(context.Rotation);
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