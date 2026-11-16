using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Quadtree;
using SkiaSharp;
using VecTiles.Common.Enums;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Renderers.Skia.Extensions;
using VecTiles.Styles.OpenMapTiles.Extensions;

namespace VecTiles.Renderers.Skia;

/// <summary>
/// Renders icon line symbols on a Skia canvas.
/// </summary>
public static class IconLineSymbolRenderer
{
    private static readonly SKPaint _paint = new SKPaint { IsAntialias = true, Color = SKColors.White };
    private static readonly SKSamplingOptions SamplingOptions = new SKSamplingOptions(new SKCubicResampler(0.5f, 0.5f));
    private static readonly SKPaint DebugPaint = new SKPaint { Color = SKColors.Aqua, StrokeWidth = 1, IsStroke = true };

    /// <summary>
    /// Draws an icon on the specified canvas at the given screen coordinates.
    /// </summary>
    /// <param name="canvas">The Skia canvas to draw on.</param>
    /// <param name="context">The evaluation context for styling.</param>
    /// <param name="symbol">The icon line symbol to draw.</param>
    /// <param name="screenX">The X coordinate on the screen.</param>
    /// <param name="screenY">The Y coordinate on the screen.</param>
    /// <param name="iconRotation">The rotation of the icon in degrees.</param>
    /// <param name="showValidBorders">Whether to draw debug borders around the symbol.</param>
    public static void DrawIcon(SKCanvas canvas, EvaluationContext context, IconLineSymbol symbol, double screenX,
        double screenY, float iconRotation, bool showValidBorders = false)
    {
        _paint.Color = SKColors.White.WithAlpha((byte)(symbol.Opacity?.Invoke(context) * 255 ?? 255));

        if (symbol.ColorFilter != null)
        {
            _paint.ColorFilter = SKColorFilter.CreateColorMatrix(symbol.ColorFilter(context));
        }

        var image = (SKBitmap)symbol.Icon.Native!;
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
        canvas.Scale(1f / context.Scale * symbol.Scale / symbol.Icon.PixelRatio);

        var rotation = iconRotation;

        rotation = (rotation + 360) % 360;

        canvas.RotateDegrees(rotation, 0f, 0f);

        if (symbol.KeepUpright && rotation > 90 && rotation < 270)
        {
            canvas.RotateDegrees(180f,
                (0.5f - anchorX) * imgWidth,
                (0.5f - anchorY) * imgHeight);
        }

        canvas.DrawBitmap(image,
            -anchorX * imgWidth + offsetX,
            -anchorY * imgHeight + offsetY,
            _paint);

        canvas.Restore();

        if (showValidBorders)
        {
            canvas.DrawRect(
                new SKRect((float)symbol.ScreenEnvelope!.MinX, (float)symbol.ScreenEnvelope!.MinY,
                (float)symbol.ScreenEnvelope!.MaxX, (float)symbol.ScreenEnvelope!.MaxY), DebugPaint);
        }
    }

    /// <summary>
    /// Checks if there is space to draw a single icon symbol at the specified position on a path.
    /// </summary>
    /// <param name="canvas">The Skia canvas to check against.</param>
    /// <param name="context">The evaluation context for styling.</param>
    /// <param name="path">The path along which the symbol is placed.</param>
    /// <param name="startPos">The starting position on the path.</param>
    /// <param name="symbol">The icon line symbol to check.</param>
    /// <param name="tree">The quadtree of existing symbols for collision detection.</param>
    /// <param name="screenX">The X coordinate on the screen.</param>
    /// <param name="screenY">The Y coordinate on the screen.</param>
    /// <param name="showUnvalidBorders">Whether to draw debug borders for collisions.</param>
    /// <returns>A <see cref="SymbolPlacementStatus"/> indicating if the symbol can be placed.</returns>
    public static SymbolPlacementStatus CheckForSingleSpace(SKCanvas canvas,
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
            return SymbolPlacementStatus.NotInitialized;
        }

        // Check if there is enough space to fit the symbol on the next line segment
        if (!path.CanFitOnNextLineSegment((float)startPos, (float)(symbol.Envelope.MaxX - symbol.Envelope.MinX)))
        {
            return SymbolPlacementStatus.Collision;
        }

        // Save for later use
        var screenEnvelope = symbol.Envelope.Copy();

        // Move symbol's envelope to the screen position
        if (context.Rotation != 0.0)
        {
            screenEnvelope.RotateDegrees(context.Rotation);
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

            if (showUnvalidBorders && symbol.Name != otherSymbol.Name)
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
    /// Creates an envelope for the icon line symbol based on its dimensions and transformations.
    /// </summary>
    /// <param name="symbol">The icon line symbol.</param>
    /// <param name="context">The evaluation context for styling.</param>
    /// <returns>The calculated envelope, or null if the symbol has no icon.</returns>
    private static Envelope? CreateEnvelope(IconLineSymbol symbol, EvaluationContext context)
    {
        if (symbol.Icon == null)
        {
            return null;
        }

        // Use cached envelope if available
        if (symbol.Envelope is not null)
        {
            return symbol.Envelope;
        }

        var width = (int)Math.Ceiling(symbol.Icon.Width * symbol.Scale);
        var height = (int)Math.Ceiling(symbol.Icon.Height * symbol.Scale);

        if (symbol.Icon.Native is null)
        {
            symbol.Icon.Atlas.Native ??= SKImage.FromEncodedData(symbol.Icon.Atlas.Binary);

            var spriteBitmap = new SKBitmap(width, height,
                ((SKImage)symbol.Icon.Atlas.Native).ColorType,
                ((SKImage)symbol.Icon.Atlas.Native).AlphaType);
            var spriteRect = new SKRect(symbol.Icon.X, symbol.Icon.Y,
                symbol.Icon.X + symbol.Icon.Width,
                symbol.Icon.Y + symbol.Icon.Height);

            using (var canvas = new SKCanvas(spriteBitmap))
            {
                canvas.DrawImage((SKImage)symbol.Icon.Atlas.Native, spriteRect,
                    new SKRect(0, 0, width, height)
                );
            }

            symbol.Icon.Native = spriteBitmap;
        }

        var anchor = new Point(symbol.Anchor.X * width, symbol.Anchor.Y * height);
        var offset = new Point(anchor.X + symbol.Offset.X, anchor.Y + symbol.Offset.Y);

        // Calculate the rough envelope of the icon
        var envelope = new Envelope(offset.X - symbol.Padding, offset.X + width + symbol.Padding,
            offset.Y - symbol.Padding, offset.Y + height + symbol.Padding);

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
                var rotationRad = context.Rotation * Math.PI / 180.0;
                var cos = Math.Cos(rotationRad);
                var sin = Math.Sin(rotationRad);
                var x = translate.X * cos - translate.Y * sin;
                var y = translate.X * sin + translate.Y * cos;
                translate = new Point((float)x, (float)y);
            }

            envelope.Translate(translate.X, translate.Y);
        }

        return envelope;
    }
}
