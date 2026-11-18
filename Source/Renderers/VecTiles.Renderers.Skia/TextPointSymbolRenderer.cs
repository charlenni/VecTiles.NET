using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Quadtree;
using SkiaSharp;
using VecTiles.Common.Enums;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Renderers.Common.Interfaces;
using VecTiles.Renderers.Skia.Extensions;
using VecTiles.Renderers.Skia.Primitives;
using VecTiles.Styles.OpenMapTiles.Extensions;

namespace VecTiles.Renderers.Skia;

/// <summary>
/// Renders text point symbols on a Skia canvas.
/// </summary>
public class TextPointSymbolRenderer : ISymbolRenderer
{
    private static readonly SKPaint _paint = new SKPaint();
    private static readonly SKPaint DebugPaint = new SKPaint { Color = SKColors.Blue, StrokeWidth = 1, IsStroke = true };

    /// <summary>
    /// Draws text on the specified canvas at the given screen coordinates.
    /// </summary>
    /// <param name="canvas">The Skia canvas to draw on.</param>
    /// <param name="context">The evaluation context for styling.</param>
    /// <param name="symbol">The text point symbol to draw.</param>
    /// <param name="screenX">The X coordinate on the screen.</param>
    /// <param name="screenY">The Y coordinate on the screen.</param>
    /// <param name="rotation">The rotation angle in degrees.</param>
    /// <param name="showValidBorders">Whether to draw debug borders around the symbol.</param>
    public static void DrawText(SKCanvas canvas, EvaluationContext context, TextPointSymbol symbol, double screenX, double screenY, double rotation, bool showValidBorders = false)
    {
        var anchorX = (float)symbol.Anchor.X;
        var anchorY = (float)symbol.Anchor.Y;
        var offsetX = (float)symbol.Offset.X;
        var offsetY = (float)symbol.Offset.Y;
        var translate = symbol.Translate?.Invoke(context) ?? new Point(0, 0);
        var transX = (float)translate.X;
        var transY = (float)translate.Y;
        var transAnchor = symbol.TranslateAnchor?.Invoke(context) ?? MapAlignment.Map;

        // TranslateAnchor can only be Map or Viewport
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
        canvas.Scale(1f / context.Scale);

        var totalRotation = symbol.Rotation;

        if (symbol.RotationAlignment == MapAlignment.Map)
        {
            totalRotation += context.Rotation;
        }

        totalRotation = (totalRotation + 360) % 360;

        canvas.RotateDegrees(totalRotation, 0f, 0f);

        var text = (RenderedText)symbol.Native;

        var textStyle = text.TextStyle;

        textStyle.TextColor = symbol.Color.Invoke(context).ToSKColor().WithAlpha((byte)(symbol.Opacity.Invoke(context) * 255f));
        textStyle.HaloBlur = symbol.HaloBlur.Invoke(context);
        textStyle.HaloColor = symbol.HaloColor.Invoke(context).ToSKColor();
        // RichTextKit strokes the halo centered on the glyph outline (half inwards,
        // half outwards). The OMT spec interprets halo-width as the distance to the
        // outline outwards, so double the width to compensate.
        textStyle.HaloWidth = 2f * symbol.HaloWidth.Invoke(context);

        // Translate to the correct position, respecting the anchor and offset
        canvas.Translate(-anchorX * text.MeasuredWidth + offsetX,
            -anchorY * text.MeasuredHeight - text.Text.MeasuredPadding.Top + offsetY);

        // Correction for RichTextKit drawing with MaxWidth coordinates but using only real text size.
        // Respect the text's left-right correction.
        canvas.Translate(-text.LeftRightCorrection, 0);

        if (symbol.KeepUpright && rotation is > 90 and < 270)
        {
            canvas.RotateDegrees(180f,
                0.5f * text.MeasuredWidth,
                0.5f * text.MeasuredHeight);
        }

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
    /// Checks if there is space to draw the text symbol at the specified screen coordinates.
    /// </summary>
    /// <param name="canvas">The Skia canvas to check against.</param>
    /// <param name="context">The evaluation context for styling.</param>
    /// <param name="symbol">The text point symbol to check.</param>
    /// <param name="tree">The quadtree of existing symbols for collision detection.</param>
    /// <param name="screenX">The X coordinate on the screen.</param>
    /// <param name="screenY">The Y coordinate on the screen.</param>
    /// <param name="rotation">The rotation angle in degrees.</param>
    /// <param name="showInvalidBorders">Whether to draw debug borders for collisions.</param>
    /// <returns>A <see cref="SymbolPlacementStatus"/> indicating if the symbol can be placed.</returns>
    public static SymbolPlacementStatus CheckForSpace(SKCanvas canvas, EvaluationContext context,
        TextPointSymbol symbol, Quadtree<ISymbol> tree, double screenX, double screenY, double rotation, bool showInvalidBorders)
    {
        symbol.Envelope ??= CreateEnvelope(symbol, context);

        if (symbol.Envelope is null)
        {
            return SymbolPlacementStatus.NotInitialized;
        }

        // Save for later use
        var screenEnvelope = symbol.Envelope.Copy();

        // Calculate the rotation
        var totalRotation = symbol.Rotation;

        if (symbol.RotationAlignment == MapAlignment.Map)
        {
            totalRotation += context.Rotation;
        }

        totalRotation = (totalRotation + 360) % 360;

        // Rotate around the symbol's anchor point at (0, 0), matching the canvas
        // rotation in DrawText, before translating to the screen position
        screenEnvelope.RotateDegreesAroundPoint(totalRotation, 0, 0);

        // Calculate the translation
        if (symbol.Translate != null)
        {
            var translate = symbol.Translate?.Invoke(context) ?? new Point(0, 0);
            var translateAnchor = symbol.TranslateAnchor?.Invoke(context) ?? MapAlignment.Map;

            if (translateAnchor == MapAlignment.Map)
            {
                var radRotation = context.Rotation * Math.PI / 180.0;
                var cos = Math.Cos(radRotation);
                var sin = Math.Sin(radRotation);
                var x = translate.X * cos - translate.Y * sin;
                var y = translate.X * sin + translate.Y * cos;
                translate = new Point((float)x, (float)y);
            }

            screenEnvelope.Translate(translate.X, translate.Y);
        }

        // Move symbol's envelope to the screen position
        screenEnvelope.Translate(screenX, screenY);

        var symbols = tree.Query(screenEnvelope);

        foreach (var other in symbols)
        {
            if (other is not Symbol otherSymbol)
            {
                continue;
            }

            if (otherSymbol.Envelope == null)
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
    /// <param name="useCached">Whether to use a cached envelope if available.</param>
    /// <returns>Envelope for a valid symbol, otherwise null.</returns>
    private static Envelope? CreateEnvelope(TextPointSymbol symbol, EvaluationContext context, bool useCached = true)
    {
        if (symbol.Text == null)
        {
            return null;
        }

        // Use cached envelope if available
        if (symbol.Envelope is not null && useCached)
        {
            return symbol.Envelope;
        }

        var fontSize = symbol.FontSize.Invoke(context);
        var maxWidth = symbol.MaxWidth.Invoke(context, fontSize);

        // Layout is expensive; identical texts with identical layout parameters
        // share one measured RenderedText
        symbol.Native = RenderedTextCache.GetOrCreate(symbol.Text, symbol.StyleName, symbol.FontNames,
            fontSize, maxWidth, symbol.Alignment.ToTextAlignment(), (int)symbol.Direction);

        var text = (RenderedText)symbol.Native;

        var anchor = new SKPoint((float)(-symbol.Anchor.X * text.MeasuredWidth), (float)(-symbol.Anchor.Y * text.MeasuredHeight));
        var offset = new SKPoint((float)(anchor.X + symbol.Offset.X), (float)(anchor.Y + symbol.Offset.Y));
        var padding = symbol.Padding;

        // Calculate the rough envelope of text, respecting overhang (e.g., from italic characters)
        var envelope = new Envelope(offset.X - padding - text.Text.MeasuredOverhang.Left,
            offset.X + padding + text.MeasuredWidth + text.Text.MeasuredOverhang.Right,
            offset.Y - padding - text.Text.MeasuredOverhang.Top,
            offset.Y + padding + text.MeasuredHeight + text.Text.MeasuredOverhang.Bottom);

        return envelope;
    }
}
