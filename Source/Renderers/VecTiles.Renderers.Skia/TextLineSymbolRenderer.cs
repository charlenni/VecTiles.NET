using System.Collections.Concurrent;
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
/// Placement of a text label along a path.
/// </summary>
/// <param name="Reversed">Text is drawn against the direction of the path (keep upright).</param>
/// <param name="DrawStart">Distance along the drawing path where the first glyph starts
/// (on the reversed path for reversed labels).</param>
/// <param name="PathLength">Length of the contour the label is placed on.</param>
/// <param name="Boxes">Collision boxes of the label in screen coordinates, one per
/// step along the path, rotated to the local tangent.</param>
public sealed record PathTextPlacement(bool Reversed, float DrawStart, float PathLength,
    IReadOnlyList<Envelope> Boxes);

/// <summary>
/// Renders text line symbols on a Skia canvas. Text of line symbols is drawn
/// along the line with every character rotated to the local tangent of the path
/// (like MapLibre renders labels with symbol-placement: line).
/// </summary>
public static class TextLineSymbolRenderer
{
    private static readonly SKPaint DebugPaint = new SKPaint { Color = SKColors.Red, StrokeWidth = 1, IsStroke = true };

    /// <summary>
    /// Cache of resolved typefaces, so that font matching is not repeated for
    /// every drawn label.
    /// </summary>
    private static readonly ConcurrentDictionary<(string Family, int Weight, int Width, bool Italic), SKTypeface> TypefaceCache = new();

    /// <summary>
    /// Draws text along a path on the specified canvas.
    /// </summary>
    /// <param name="canvas">The Skia canvas to draw on.</param>
    /// <param name="context">The evaluation context for styling.</param>
    /// <param name="symbol">The text line symbol to draw.</param>
    /// <param name="path">The path along which to draw the text.</param>
    /// <param name="placement">Placement of the label on the path, as returned by CheckForSingleSpace.</param>
    /// <param name="showValidBorders">Whether to draw debug borders around the symbol.</param>
    /// <param name="fontSizeRatio">Ratio of the drawn font size at the fractional zoom to the layout font size of the tile zoom.</param>
    public static void DrawTextOnPath(SKCanvas canvas, EvaluationContext context, TextLineSymbol symbol,
        SKPath path, PathTextPlacement placement, bool showValidBorders = false, float fontSizeRatio = 1f)
    {
        var text = (RenderedText)symbol.Native!;

        var fontSize = symbol.FontSize.Invoke(context);

        var haloWidth = 2f * symbol.HaloWidth.Invoke(context);

        // Color of the halo and the text, halo is drawn first (stroked around the
        // glyphs, see the doubled width in the RichTextKit renderer), then the fill
        using var haloPaint = new SKPaint
        {
            Typeface = ResolveTypeface(text),
            TextSize = fontSize,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = haloWidth,
            StrokeJoin = SKStrokeJoin.Round,
            Color = symbol.HaloColor.Invoke(context).ToSKColor(),
        };

        using var fillPaint = new SKPaint
        {
            Typeface = haloPaint.Typeface,
            TextSize = fontSize,
            IsAntialias = true,
            Color = symbol.Color.Invoke(context).ToSKColor().WithAlpha((byte)(symbol.Opacity.Invoke(context) * 255f)),
        };

        // Vertical centering: FontMetrics.Ascent is negative, so this shifts the
        // baseline below the path by half the line height and the character
        // centers sit on the path
        var offsetFromPath = (fillPaint.FontMetrics.Descent - fillPaint.FontMetrics.Ascent) / 2f;

        // The glyphs are drawn exactly over the collision boxes: the draw start
        // was computed in the check from the anchor and the padded envelope
        var drawPath = path;
        var start = placement.DrawStart;

        if (placement.Reversed)
        {
            // Draw against the path direction, so the text keeps upright; the
            // draw start was already mirrored to the reversed path
            drawPath = path.Reverse();
        }

        canvas.DrawTextOnPath(symbol.Text, drawPath, start, offsetFromPath, haloPaint, fillPaint);

        if (showValidBorders)
        {
            // The borders wrap the drawn glyphs: the drawn label runs from the
            // draw start over the width of the drawn text (layout width scaled
            // by the font size ratio) on the drawn path. The per-step boxes of
            // the check are redistributed over this run, so the borders frame
            // the glyphs however the label is mirrored, scaled or reshaped.
            using var borderMeasure = new SKPathMeasure(drawPath, false);
            var labelWidth = text.MeasuredWidth * fontSizeRatio;
            var step = (float)(labelWidth / placement.Boxes.Count);

            for (var i = 0; i < placement.Boxes.Count; i++)
            {
                var center = placement.DrawStart + (i + 0.5f) * step;

                if (!borderMeasure.GetPositionAndTangent(center, out var position, out var tangentVec))
                {
                    continue;
                }

                var box = placement.Boxes[i];

                var width = (float)((box.MaxX - box.MinX) / 2 * fontSizeRatio);
                var height = (float)((box.MaxY - box.MinY) / 2 * fontSizeRatio);

                var border = new Envelope(position.X - width, position.X + width,
                    position.Y - height, position.Y + height);

                var angle = (float)(Math.Atan2(tangentVec.Y, tangentVec.X) * 180 / Math.PI);

                border.RotateDegreesAroundPoint(angle, position.X, position.Y);

                canvas.DrawRect(
                    new SKRect((float)border.MinX, (float)border.MinY,
                        (float)border.MaxX, (float)border.MaxY), DebugPaint);
            }
        }
    }

    /// <summary>
    /// Checks if a text label fits on the path starting from the given position:
    /// the whole label must fit on the current contour (measured along the path,
    /// not along straight segments) and none of its per-step collision boxes may
    /// intersect a symbol that was already placed.
    /// </summary>
    /// <param name="canvas">Canvas with clipping bounds.</param>
    /// <param name="context">Context for creating the envelope.</param>
    /// <param name="path">Single contour of the path on which this symbol should be placed.</param>
    /// <param name="startPos">Anchor position on the path where the label starts.</param>
    /// <param name="symbol">Symbol that should be placed on the path.</param>
    /// <param name="tree">Tree with all other already placed symbols.</param>
    /// <param name="showInvalidBorders">Flag indicating if invalid borders should be drawn.</param>
    /// <param name="placement">Receives the placement of the label if it fits.</param>
    /// <returns>A <see cref="SymbolPlacementStatus"/> indicating if the symbol can be placed.</returns>
    public static SymbolPlacementStatus CheckForSingleSpace(SKCanvas canvas, EvaluationContext context, SKPath path,
        double startPos, TextLineSymbol symbol, Quadtree<ISymbol> tree, bool showInvalidBorders,
        out PathTextPlacement? placement)
    {
        placement = null;

        symbol.Envelope ??= CreateEnvelope(symbol, context);

        if (symbol.Envelope is null)
        {
            return SymbolPlacementStatus.NotInitialized;
        }

        var width = (float)(symbol.Envelope.MaxX - symbol.Envelope.MinX);
        var height = (float)(symbol.Envelope.MaxY - symbol.Envelope.MinY);

        if (width <= 0 || height <= 0)
        {
            return SymbolPlacementStatus.NotInitialized;
        }

        using var pathMeasure = new SKPathMeasure(path);
        var length = pathMeasure.Length;

        pathMeasure.GetPositionAndTangent((float)startPos, out _, out var anchorTangent);

        // With KeepUpright, draw against the path direction when the tangent
        // points leftwards, so the characters do not stand on their head
        var reversed = symbol.KeepUpright && Math.Sign(anchorTangent.X) < 0;

        // The glyph run relative to the anchor starts at the anchor position
        // (anchor 0.5 centers the label) plus the horizontal text offset
        var text = (RenderedText)symbol.Native!;
        var measuredWidth = text.MeasuredWidth;
        var glyphLead = (float)(-symbol.Anchor.X * measuredWidth + symbol.Offset.X);

        // The padded envelope runs along the path symmetric to the reading
        // direction: forward from the anchor shifted by the envelope's left
        // edge, or mirrored for flipped labels. Collision boxes and drawn
        // glyphs must cover exactly this run.
        var runStart = reversed
            ? (float)(startPos - symbol.Envelope.MaxX)
            : (float)(startPos + symbol.Envelope.MinX);

        var drawStart = reversed
            ? (float)(length - startPos + glyphLead)
            : (float)(startPos + glyphLead);

        if (runStart < 0 || runStart + width > length)
        {
            return SymbolPlacementStatus.Collision;
        }

        // One collision box per step along the path, rotated to the local tangent
        // (MapLibre checks one box per glyph; with even in path direction even
        // sized boxes, curved labels do not block more space than they occupy)
        var step = Math.Min(width, Math.Max(8f, height));
        var count = Math.Max(1, (int)Math.Ceiling(width / step));
        var boxWidth = width / count;

        var boxes = new List<Envelope>(count);

        var maxAngle = symbol.MaxAngle?.Invoke(context) ?? 45f;
        var lastAngle = double.NaN;

        // Fine sampling of the tangents, so that sharp bends between the box
        // centers are not averaged away
        var angleStep = Math.Min(4f, boxWidth);

        for (var i = 0; i < count; i++)
        {
            var center = (float)(runStart + (i + 0.5) * boxWidth);

            if (!pathMeasure.GetPositionAndTangent(center, out var position, out var tangentVec))
            {
                return SymbolPlacementStatus.Collision;
            }

            // Drop labels on lines that bend sharper than the maximum angle
            // change per step (MapLibre's text-max-angle)
            for (var sample = i == 0 ? runStart + angleStep / 2 : center - boxWidth / 2;
                sample <= center + boxWidth / 2 + 0.5;
                sample += angleStep)
            {
                pathMeasure.GetPositionAndTangent((float)sample, out _, out var sampleTangent);

                var sampleAngle = Math.Atan2(sampleTangent.Y, sampleTangent.X) * 180 / Math.PI;

                if (!double.IsNaN(lastAngle) && Math.Abs(NormalizeAngle(sampleAngle - lastAngle)) >= maxAngle)
                {
                    return SymbolPlacementStatus.Collision;
                }

                lastAngle = sampleAngle;
            }

            var angle = Math.Atan2(tangentVec.Y, tangentVec.X) * 180 / Math.PI;

            var box = new Envelope(position.X - boxWidth / 2, position.X + boxWidth / 2,
                position.Y - height / 2, position.Y + height / 2);

            box.RotateDegreesAroundPoint(angle, position.X, position.Y);

            boxes.Add(box);
        }

        // Union of all boxes for the query, the exact check runs per box
        var union = boxes[0].Copy();

        foreach (var box in boxes.Skip(1))
        {
            union.ExpandToInclude(box);
        }

        var symbols = tree.Query(union);

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

            if (!union.Intersects(otherSymbol.ScreenEnvelope))
            {
                continue;
            }

            var intersectsLabel = boxes.Any(box => box.Intersects(otherSymbol.ScreenEnvelope!));

            if (!intersectsLabel)
            {
                continue;
            }

            if (symbol.AllowOthers && otherSymbol.AllowOthers)
            {
                continue;
            }

            if (showInvalidBorders && symbol.Name != otherSymbol.Name)
            {
                canvas.DrawRect(new SKRect((float)union.MinX, (float)union.MinY,
                    (float)union.MaxX, (float)union.MaxY), DebugPaint);
            }

            return SymbolPlacementStatus.Collision;
        }

        // Union envelope as screen envelope of the symbol, the per-box envelopes
        // are inserted into the quadtree by the caller after drawing
        symbol.ScreenEnvelope = union;

        placement = new PathTextPlacement(reversed, drawStart, length, boxes);

        return SymbolPlacementStatus.Success;
    }

    /// <summary>
    /// Normalizes an angle to [-180, 180].
    /// </summary>
    private static double NormalizeAngle(double angle)
    {
        angle %= 360;

        if (angle > 180)
        {
            angle -= 360;
        }

        if (angle < -180)
        {
            angle += 360;
        }

        return angle;
    }

    /// <summary>
    /// Resolves the Skia typeface for the cached text once per font signature.
    /// </summary>
    private static SKTypeface ResolveTypeface(RenderedText text)
    {
        var style = text.TextStyle;

        var key = (style.FontFamily, (int)style.FontWeight, (int)style.FontWidth, style.FontItalic);

        return TypefaceCache.GetOrAdd(key, static key =>
        {
            var fontStyle = new SKFontStyle((SKFontStyleWeight)key.Weight,
                (SKFontStyleWidth)key.Width,
                key.Italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright);

            return SKFontManager.Default.MatchFamily(key.Family, fontStyle);
        });
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

        var fontSize = symbol.FontSize.Invoke(context);
        var maxWidth = symbol.MaxWidth.Invoke(context, fontSize);

        // Layout is expensive; identical texts with identical layout parameters
        // share one measured RenderedText
        symbol.Native = RenderedTextCache.GetOrCreate(symbol.Text, symbol.StyleName, symbol.FontNames,
            fontSize, symbol.LineHeight, maxWidth, symbol.Alignment.ToTextAlignment(), (int)symbol.Direction);

        var text = (RenderedText)symbol.Native;

        var anchor = new SKPoint((float)(-symbol.Anchor.X * text.MeasuredWidth), (float)(-symbol.Anchor.Y * text.MeasuredHeight));
        // OMT defines text-offset for line-placed symbols in the rotated frame of
        // the text, where y grows upwards (no y-flip like for point placement).
        // Screen coordinates grow downwards, so a positive offset moves the label
        // up, matching the reference implementations (maplibre-gl-js / Maputnik).
        var offset = new SKPoint((float)(anchor.X + symbol.Offset.X), (float)(anchor.Y - symbol.Offset.Y));
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
