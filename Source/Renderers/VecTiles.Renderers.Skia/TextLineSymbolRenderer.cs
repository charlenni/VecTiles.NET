using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Quadtree;
using SkiaSharp;
using Topten.RichTextKit;
using VecTiles.Common.Enums;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Renderers.Skia.Extensions;
using VecTiles.Renderers.Skia.Primitives;
using VecTiles.Styles.OpenMapTiles.Extensions;

namespace VecTiles.Renderers.Skia;

public static class TextLineSymbolRenderer
{
    private static readonly SKPaint DebugPaint = new SKPaint { Color = SKColors.Red, StrokeWidth = 1, IsStroke = true };

    public static void DrawText(SKCanvas canvas, EvaluationContext context, TextLineSymbol symbol, double screenX, double screenY, double rotation, bool showValidBorders = false)
    {
        SKPaint paint = new SKPaint();

        canvas.Save();

        canvas.Translate((float)screenX, (float)screenY);
        canvas.Scale(1f / context.Scale);
        
        if (symbol.RotationAlignment == MapAlignment.Map)
        {
            canvas.RotateDegrees((float)rotation);
        }

        var text = (RenderedText)symbol.Native;

        var textStyle = text.TextStyle;

        textStyle.TextColor = symbol.Color.Invoke(context).ToSKColor().WithAlpha((byte)(symbol.Opacity.Invoke(context) * 255f));
        textStyle.HaloBlur = symbol.HaloBlur.Invoke(context);
        textStyle.HaloColor = symbol.HaloColor.Invoke(context).ToSKColor();
        textStyle.HaloWidth = symbol.HaloWidth.Invoke(context);

        // Translate to right position respecting the correction (leading space before TextBlocks text starts)
        //canvas.Translate((float)(symbol.Anchor.X * text.MeasuredWidth) - text.Text.MeasuredPadding.Left, (float)(symbol.Anchor.Y * text.MeasuredHeight - text.Text.MeasuredPadding.Top));
        canvas.Translate((float)symbol.Envelope.MinX, (float)symbol.Envelope.MinY);

        // Correction, because RichTextKit draws with MaxWidth coordinates but we use only real text size
        canvas.Translate(symbol.Padding - text.LeftRightCorrection, symbol.Padding);
        // Draw text with RichTextKit
        text.Text.Paint(canvas);

        canvas.Restore();

        if (showValidBorders)
        {
            canvas.DrawRect(
                new SKRect((float) symbol.ScreenEnvelope!.MinX, (float) symbol.ScreenEnvelope!.MinY, (float) symbol.ScreenEnvelope!.MaxX,
                    (float) symbol.ScreenEnvelope!.MaxY), DebugPaint);
        }
    }
    
    public static void DrawTextOnPath(SKCanvas canvas, EvaluationContext context, TextLineSymbol symbol, SKPath path, double screenX, double screenY, bool showValidBorders)
    {
        SKPaint paint = new SKPaint();

        var text = (RenderedText) symbol.Native;
        text.Text.Alignment = symbol.Alignment.ToTextAlignment(); 
        
        var textStyle = (Style)text.Text.GetStyleAtOffset(0);
        var font = new SKFont();

        canvas.DrawTextOnPath("Test", path, new SKPoint(0, 0), font, paint);
    }

    /// <summary>
    /// Check, if one symbol fits on the path. Respects visible in clipped bounds,
    /// enough space on the path and no other symbol occupies the place.
    /// </summary>
    /// <param name="canvas">Canvas with clipping bounds</param>
    /// <param name="context">Context for creating envelope</param>
    /// <param name="path">Path on which this symbol should placed</param>
    /// <param name="startPos">Start position on path where the first symbol should placed</param>
    /// <param name="symbol">Symbol that should be placed on the path</param>
    /// <param name="tree">Tree with all other already placed symbols</param>
    /// <param name="screenX">X position for creating screen envelope of symbol</param>
    /// <param name="screenY">Y position for creating screen envelope of symbol</param>
    /// <param name="rotation">Rotation of screen for creating screen envelope of symbol</param>
    /// <param name="showInvalidBorders">Flag, if invalid borders should be drawn</param>
    /// <returns></returns>
    public static bool CheckForSingleSpace(SKCanvas canvas, EvaluationContext context, SKPath path, double startPos, 
        TextLineSymbol symbol, Quadtree<ISymbol> tree, double screenX, double screenY, double rotation, bool showInvalidBorders)
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

            if (symbol.AllowOthers  && otherSymbol.AllowOthers)
            {
                continue;
            }
            
            if (showInvalidBorders && symbol.Name != otherSymbol.Name)
            {
                canvas.DrawRect(new SKRect((float)screenEnvelope.MinX, (float)screenEnvelope.MinY, (float)screenEnvelope.MaxX, (float)screenEnvelope.MaxY), DebugPaint);
            }

            return false;
        }

        symbol.ScreenEnvelope = screenEnvelope;
        
        return true;
    }

    /// <summary>
    /// Create an envelope around text symbol.
    /// (0, 0) is the anchor point of this symbol.
    /// </summary>
    /// <param name="symbol">Symbol to create an envelope for</param>
    /// <param name="context">Context to use for this calculation</param>
    /// <returns>Envelope for a valid symbol, else null</returns>
    private static Envelope? CreateEnvelope(TextLineSymbol symbol, EvaluationContext context)
    {
        if (symbol.Text == null)
        {
            return null;
        }

        // We already calculated for this symbol an envelope, so use this
        if (symbol.Envelope is not null)
        {
            return symbol.Envelope;
        }
        
        symbol.Native ??= new RenderedText(symbol.Text, FontCache.CreateFonts(symbol.StyleName, symbol.FontNames));
        
        var text = (RenderedText)symbol.Native;

        text.Text.Alignment = symbol.Alignment.ToTextAlignment(); 
        
        text.TextStyle.FontSize = symbol.FontSize.Invoke(context);
        
        // Set max width to the correct value
        text.Text.MaxWidth = symbol.MaxWidth.Invoke(context, text.TextStyle.FontSize);
        text.Text.ApplyStyle(0, text.Text.Length, text.TextStyle);

        // MeasuredWidth and MeasuredHeight need many CPU cycles, so save them for later use
        text.MeasuredWidth = text.Text.MeasuredWidth;
        text.MeasuredHeight = text.Text.MeasuredHeight;
        
        // We don't want any padding on the left, so remove it. Costs a second time the layout
        if ((text.Text.MeasuredPadding.Left > 0 || text.Text.MeasuredPadding.Right > 0) && text.Text.LineCount == 1)
        {
            text.Text.MaxWidth = null;
            text.MeasuredWidth = text.Text.MeasuredWidth;
            text.MeasuredHeight = text.Text.MeasuredHeight;
        }
        
        text.LeftRightCorrection = text.Text.MeasuredPadding.Left;

        var anchor = new SKPoint((float)(-symbol.Anchor.X * text.MeasuredWidth), (float)(-symbol.Anchor.Y * text.MeasuredHeight));
        var offset = new SKPoint((float)(anchor.X + symbol.Offset.X), (float)(anchor.Y + symbol.Offset.Y));
        var padding = symbol.Padding;

        // We now could calc the rough envelope of text respecting the overhang e.g. from italic characters)
        var envelope = new Envelope(offset.X - padding - text.Text.MeasuredOverhang.Left, 
            offset.X + padding + text.MeasuredWidth + text.Text.MeasuredOverhang.Right, 
            offset.Y - padding - text.Text.MeasuredPadding.Top, 
            offset.Y + padding + text.MeasuredHeight + text.Text.MeasuredOverhang.Bottom);

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
                var rot = context.Rotation * Math.PI / 180.0;
                var cos = Math.Cos(rot);
                var sin = Math.Sin(rot);
                var x = translate.X * cos - translate.Y * sin;
                var y = translate.X * sin + translate.Y * cos;
                translate = new Point((float)x, (float)y);
            }

            envelope.Translate(translate.X, translate.Y);
        }
        
        return envelope;
    }
}