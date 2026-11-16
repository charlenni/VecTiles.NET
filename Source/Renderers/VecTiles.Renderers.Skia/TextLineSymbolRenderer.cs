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

/// <summary>
/// Renders text line symbols on a Skia canvas.
/// </summary>
public static class TextLineSymbolRenderer
{
    private static readonly SKPaint DebugPaint = new SKPaint { Color = SKColors.Red, StrokeWidth = 1, IsStroke = true };

    /// <summary>
    /// Draws text on the specified canvas at the given screen coordinates.
    /// </summary>
    /// <param name="canvas">The Skia canvas to draw on.</param>
    /// <param name="context">The evaluation context for styling.</param>
    /// <param name="symbol">The text line symbol to draw.</param>
    /// <param name="screenX">The X coordinate on the screen.</param>
    /// <param name="screenY">The Y coordinate on the screen.</param>
    /// <param name="rotation">The rotation angle in degrees.</param>
    /// <param name="showValidBorders">Whether to draw debug borders around the symbol.</param>
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

        // Translate to the correct position, respecting the correction (leading space before TextBlocks text starts)
        canvas.Translate((float)symbol.Envelope.MinX, (float)symbol.Envelope.MinY);

        // Correction for RichTextKit drawing with MaxWidth coordinates but using only real text size
        canvas.Translate(symbol.Padding - text.LeftRightCorrection, symbol.Padding);

        // Draw the text using RichTextKit
        text.Text.Paint(canvas);

        canvas.Restore();

        if (showValidBorders)
        {
            canvas.DrawRect(
                new SKRect((float)symbol.ScreenEnvelope!.MinX, (float)symbol.ScreenEnvelope!.MinY,
                    (float)symbol.ScreenEnvelope!.MaxX, (float)symbol.ScreenEnvelope!.MaxY), DebugPaint);
        }
    }

    /// <summary>
    /// Draws text along a path on the specified canvas.
    /// </summary>
    /// <param name="canvas">The Skia canvas to draw on.</param>
    /// <param name="context">The evaluation context for styling.</param>
    /// <param name="symbol">The text line symbol to draw.</param>
    /// <param name="path">The path along which to draw the text.</param>
    /// <param name="screenX">The X coordinate on the screen.</param>
    /// <param name="screenY">The Y coordinate on the screen.</param>
    /// <param name="showValidBorders">Whether to draw debug borders around the symbol.</param>
    public static void DrawTextOnPath(SKCanvas canvas, EvaluationContext context, TextLineSymbol symbol, SKPath path, double screenX, double screenY, bool showValidBorders)
    {
        SKPaint paint = new SKPaint();

        var text = (RenderedText)symbol.Native;
        text.Text.Alignment = symbol.Alignment.ToTextAlignment();

        var textStyle = (Style)text.Text.GetStyleAtOffset(0);
        var font = new SKFont();

        canvas.DrawTextOnPath(symbol.Text, path, new SKPoint((float)screenX, (float)screenY), font, paint);
    }

    /// <summary>
    /// Checks if a symbol fits on the path, respecting visible clipped bounds,
    /// enough space on the path, and no other symbol occupying the space.
    /// </summary>
    /// <param name="canvas">Canvas with clipping bounds.</param>
    /// <param name="context">Context for creating the envelope.</param>
    /// <param name="path">Path on which this symbol should be placed.</param>
    /// <param name="startPos">Start position on the path where the first symbol should be placed.</param>
    /// <param name="symbol">Symbol that should be placed on the path.</param>
    /// <param name="tree">Tree with all other already placed symbols.</param>
    /// <param name="screenX">X position for creating the screen envelope of the symbol.</param>
    /// <param name="screenY">Y position for creating the screen envelope of the symbol.</param>
    /// <param name="rotation">Rotation of the screen for creating the screen envelope of the symbol.</param>
    /// <param name="showInvalidBorders">Flag indicating if invalid borders should be drawn.</param>
    /// <returns>A <see cref="SymbolPlacementStatus"/> indicating if the symbol can be placed.</returns>
    public static SymbolPlacementStatus CheckForSingleSpace(SKCanvas canvas, EvaluationContext context, SKPath path, double startPos,
        TextLineSymbol symbol, Quadtree<ISymbol> tree, double screenX, double screenY, double rotation, bool showInvalidBorders)
    {
        symbol.Envelope ??= CreateEnvelope(symbol, context);

        if (symbol.Envelope is null)
        {
            return SymbolPlacementStatus.NotInitialized;
        }

        // Check if there is enough space to fit text on the segment
        if (!path.CanFitOnNextLineSegment((float)startPos, (float)(symbol.Envelope.MaxX - symbol.Envelope.MinX)))
        {
            return SymbolPlacementStatus.Collision;
        }

        // Save for later use
        var screenEnvelope = symbol.Envelope.Copy();

        // Move symbol's envelope to the screen position
        if (rotation != 0.0)
        {
            screenEnvelope.RotateDegrees(rotation);
        }

        screenEnvelope.Translate(screenX, screenY);

        var symbols = tree.Query(screenEnvelope);

        foreach (var other in symbols)
        {
            if (other is not Symbol otherSymbol)
            {
                continue;
            }

            if (otherSymbol.ScreenEnvelope == null)
            {
                // Expected to be non-null; skip if null
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

            if (showInvalidBorders && symbol.Name != otherSymbol.Name)
            {
                canvas.DrawRect(new SKRect((float)screenEnvelope.MinX, (float)screenEnvelope.MinY,
                    (float)screenEnvelope.MaxX, (float)screenEnvelope.MaxY), DebugPaint);
            }

            return SymbolPlacementStatus.Collision;
        }

        symbol.ScreenEnvelope = screenEnvelope;

        return SymbolPlacementStatus.Success;
    }

    /// <summary>
    /// Creates an envelope around a text symbol.
    /// (0, 0) is the anchor point of this symbol.
    /// </summary>
    /// <param name="symbol">Symbol to create an envelope for.</param>
    /// <param name="context">Context to use for this calculation.</param>
    /// <returns>Envelope for a valid symbol, otherwise null.</returns>
    private static Envelope? CreateEnvelope(TextLineSymbol symbol, EvaluationContext context)
    {
        if (symbol.Text == null)
        {
            return null;
        }

        // Use cached envelope if available
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

        // MeasuredWidth and MeasuredHeight require significant CPU cycles, so cache them for later use
        text.MeasuredWidth = text.Text.MeasuredWidth;
        text.MeasuredHeight = text.Text.MeasuredHeight;

        // Remove left/right padding for single-line text to improve alignment.
        // This requires a second layout pass.
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

        // Calculate the rough envelope of text, respecting overhang (e.g., from italic characters)
        var envelope = new Envelope(offset.X - padding - text.Text.MeasuredOverhang.Left,
            offset.X + padding + text.MeasuredWidth + text.Text.MeasuredOverhang.Right,
            offset.Y - padding - text.Text.MeasuredOverhang.Top,
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
