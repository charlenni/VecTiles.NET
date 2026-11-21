using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Quadtree;
using SkiaSharp;
using VecTiles.Common.Enums;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Renderers.Common.Interfaces;
using VecTiles.Styles.OpenMapTiles.Extensions;

namespace VecTiles.Renderers.Skia;

/// <summary>
/// Renders icon point symbols on a Skia canvas.
/// </summary>
public class IconPointSymbolRenderer : ISymbolRenderer
{
    private static readonly SKPaint Paint = new SKPaint { IsAntialias = true, Color = SKColors.White };
    private static readonly SKSamplingOptions SamplingOptions = new SKSamplingOptions(new SKCubicResampler(0.5f, 0.5f));

    // Linear sampling for drawing the icon to screen: at fractional scales and sub pixel
    // positions nearest-neighbor sampling stretches the sprite edge pixels, so baked
    // sprite borders (e.g. route shields) render thicker on some sides than on others.
    private static readonly SKSamplingOptions ScreenSampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None);
    private static readonly SKPaint DebugPaint = new SKPaint { Color = SKColors.Green, StrokeWidth = 1, IsStroke = true };

    /// <summary>
    /// Draws an icon on the specified canvas at the given screen coordinates.
    /// </summary>
    /// <param name="canvas">The Skia canvas to draw on.</param>
    /// <param name="context">The evaluation context for styling.</param>
    /// <param name="symbol">The icon point symbol to draw.</param>
    /// <param name="screenX">The X coordinate on the screen.</param>
    /// <param name="screenY">The Y coordinate on the screen.</param>
    /// <param name="showValidBorders">Whether to draw debug borders around the symbol.</param>
    public static void DrawIcon(SKCanvas canvas, EvaluationContext context, IconPointSymbol symbol, double screenX, double screenY, bool showValidBorders = false)
    {
        Paint.Color = SKColors.White.WithAlpha((byte)(symbol.Opacity?.Invoke(context) * 255 ?? 255));

        // The paint is shared between all icon drawings: the color filter of
        // the previous symbol must not leak into this one
        Paint.ColorFilter = symbol.ColorFilter != null
            ? SKColorFilter.CreateColorMatrix(symbol.ColorFilter(context))
            : null;

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

        var totalRotation = symbol.Rotation;

        if (symbol.RotationAlignment == MapAlignment.Map)
        {
            totalRotation += context.Rotation;
        }

        totalRotation = (totalRotation + 360) % 360;

        canvas.RotateDegrees(totalRotation,
            -anchorX * imgWidth,
            -anchorY * imgHeight);

        if (symbol.KeepUpright && totalRotation > 90 && totalRotation < 270)
        {
            canvas.RotateDegrees(180f,
                (0.5f - anchorX) * imgWidth,
                (0.5f - anchorY) * imgHeight);
        }

        canvas.DrawImage(image,
            -anchorX * imgWidth + offsetX * symbol.Icon.PixelRatio,
            -anchorY * imgHeight + offsetY * symbol.Icon.PixelRatio,
            ScreenSampling,
            Paint);

        canvas.Restore();

        if (showValidBorders)
        {
            // The icon is drawn 1:1 in real screen space at the given position,
            // so its envelope frames the drawn icon exactly
            canvas.DrawRect(
                new SKRect((float)(symbol.Envelope!.MinX + screenX),
                    (float)(symbol.Envelope!.MinY + screenY),
                    (float)(symbol.Envelope!.MaxX + screenX),
                    (float)(symbol.Envelope!.MaxY + screenY)), DebugPaint);
        }
    }

    /// <summary>
    /// Checks if there is space to draw the symbol at the specified screen coordinates.
    /// </summary>
    /// <param name="canvas">The Skia canvas to check against.</param>
    /// <param name="context">The evaluation context for styling.</param>
    /// <param name="symbol">The icon point symbol to check.</param>
    /// <param name="tree">The quadtree of existing symbols for collision detection.</param>
    /// <param name="screenX">The X coordinate on the screen.</param>
    /// <param name="screenY">The Y coordinate on the screen.</param>
    /// <param name="showUnvalidBorders">Whether to draw debug borders for collisions.</param>
    /// <returns>A <see cref="SymbolPlacementStatus"/> indicating if the symbol can be placed.</returns>
    public static SymbolPlacementStatus CheckForSpace(SKCanvas canvas, EvaluationContext context, IconPointSymbol symbol, Quadtree<ISymbol> tree, double screenX, double screenY, bool showUnvalidBorders)
    {
        symbol.Envelope ??= CreateEnvelope(symbol, context);

        if (symbol.Envelope is null)
        {
            return SymbolPlacementStatus.NotInitialized;
        }

        // Save for later use
        var screenEnvelope = symbol.Envelope.Copy();

        if (context.Rotation != 0.0 && symbol.RotationAlignment == MapAlignment.Map)
        {
            screenEnvelope = CreateEnvelope(symbol, context, false) ?? screenEnvelope;
        }

        // Move symbol's envelope to the screen position
        screenEnvelope.Translate(screenX, screenY);

        if (symbol.RotationAlignment != MapAlignment.Map)
        {
            // Viewport-oriented symbols rotate with the viewport. To keep the placement
            // decision independent of the map rotation, use a conservative square around
            // the anchor that covers the envelope in any orientation.
            var halfSide = Math.Max(
                Math.Max(Math.Abs(screenEnvelope.MinX - screenX), Math.Abs(screenEnvelope.MaxX - screenX)),
                Math.Max(Math.Abs(screenEnvelope.MinY - screenY), Math.Abs(screenEnvelope.MaxY - screenY)));
            screenEnvelope = new Envelope(screenX - halfSide, screenX + halfSide,
                screenY - halfSide, screenY + halfSide);
        }

        // Save for later use, also when the symbol is rejected: the rejected
        // envelope is drawn as debug border in the drawing phase
        symbol.ScreenEnvelope = screenEnvelope;

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

        return SymbolPlacementStatus.Success;
    }

    /// <summary>
    /// Creates an envelope for the symbol based on its dimensions and transformations.
    /// </summary>
    /// <param name="symbol">The icon point symbol.</param>
    /// <param name="context">The evaluation context for styling.</param>
    /// <param name="useCached">Whether to use a cached envelope if available.</param>
    /// <returns>The calculated envelope, or null if the symbol has no icon.</returns>
    private static Envelope? CreateEnvelope(IconPointSymbol symbol, EvaluationContext context, bool useCached = true)
    {
        if (symbol.Icon == null)
        {
            return null;
        }

        // Use cached envelope if available
        if (symbol.Envelope is not null && useCached)
        {
            return symbol.Envelope;
        }

        // The icon is drawn at CSS pixel dimensions (atlas pixels divided by the pixel ratio),
        // scaled by the icon size
        var width = (int)Math.Ceiling(symbol.Icon.Width / symbol.Icon.PixelRatio * symbol.Scale);
        var height = (int)Math.Ceiling(symbol.Icon.Height / symbol.Icon.PixelRatio * symbol.Scale);

        if (symbol.Icon.Native is null)
        {
            var atlasImage = (SKImage)(symbol.Icon.Atlas.Native ??= SKImage.FromEncodedData(symbol.Icon.Atlas.Binary));

            // Cut out the sprite 1:1 in atlas pixels. Never bake the scale into the cached
            // image: the sprite is shared between symbols with different icon sizes, and
            // DrawIcon applies pixel ratio and scale when drawing.
            var spriteRect = new SKRect(symbol.Icon.X, symbol.Icon.Y,
                symbol.Icon.X + symbol.Icon.Width,
                symbol.Icon.Y + symbol.Icon.Height);

            using (var spriteBitmap = new SKBitmap(symbol.Icon.Width, symbol.Icon.Height,
                       atlasImage.ColorType, atlasImage.AlphaType))
            using (var spriteCanvas = new SKCanvas(spriteBitmap))
            {
                spriteCanvas.DrawImage(atlasImage, spriteRect,
                    new SKRect(0, 0, symbol.Icon.Width, symbol.Icon.Height),
                    SamplingOptions
                );

                spriteBitmap.SetImmutable();

                symbol.Icon.Native = SKImage.FromBitmap(spriteBitmap);
            }
        }

        var anchor = new Point(symbol.Anchor.X * width, symbol.Anchor.Y * height);
        var offset = new Point(-anchor.X + symbol.Offset.X * symbol.Scale, -anchor.Y + symbol.Offset.Y * symbol.Scale);

        // Calculate the rough envelope of the icon
        var envelope = new Envelope(offset.X - symbol.Padding, offset.X + width + symbol.Padding,
            offset.Y - symbol.Padding, offset.Y + height + symbol.Padding);

        var totalRotation = symbol.Rotation + (symbol.RotationAlignment == MapAlignment.Map ? context.Rotation : 0);

        if (totalRotation != 0.0)
        {
            envelope.RotateDegreesAroundAnchor(totalRotation, symbol.Anchor.X, symbol.Anchor.Y);
        }

        if (symbol.Translate != null)
        {
            var translate = symbol.Translate?.Invoke(context) ?? new Point(0, 0);
            var translateAnchor = symbol.TranslateAnchor?.Invoke(context) ?? MapAlignment.Map;

            // TranslateAnchor can only be Map or Viewport
            translateAnchor = translateAnchor == MapAlignment.Auto ? MapAlignment.Map : translateAnchor;

            if (translateAnchor == MapAlignment.Map)
            {
                var radRotation = context.Rotation * Math.PI / 180.0;
                var cos = Math.Cos(radRotation);
                var sin = Math.Sin(radRotation);
                var x = translate.X * cos - translate.Y * sin;
                var y = translate.X * sin + translate.Y * cos;
                translate = new Point((float)x, (float)y);
            }

            envelope.Translate(translate.X, translate.Y);
        }

        return envelope;
    }
}
