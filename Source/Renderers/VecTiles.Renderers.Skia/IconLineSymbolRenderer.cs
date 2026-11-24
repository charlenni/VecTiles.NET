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
    private static readonly SKPaint DebugPaint = new SKPaint { Color = SKColors.Aqua, StrokeWidth = 1, IsStroke = true };

    // Linear sampling for drawing the icon to screen: at fractional scales and sub pixel
    // positions nearest-neighbor sampling stretches the sprite edge pixels, so baked
    // sprite borders (e.g. route shields) render thicker on some sides than on others.
    private static readonly SKSamplingOptions ScreenSampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None);

    /// <summary>
    /// Returns the width of the icon in the placement screen space (atlas pixels
    /// divided by the pixel ratio, scaled by icon-size), without creating the
    /// sprite image. Used to distribute the anchors like MapLibre does.
    /// </summary>
    public static float GetLabelLength(IconLineSymbol symbol)
    {
        return symbol.Icon == null
            ? 0f
            : symbol.Icon.Width / symbol.Icon.PixelRatio * symbol.Scale;
    }

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
        // Local paint: the color and the color filter depend on the symbol,
        // and icon drawings run concurrently (parallel style layers, parallel
        // test collections) — a shared paint was mutated in between and the
        // icon blended with the alpha of another concurrently drawn symbol
        using var paint = new SKPaint { IsAntialias = true };
        paint.Color = SKColors.White.WithAlpha((byte)(symbol.Opacity?.Invoke(context) * 255 ?? 255));
        paint.ColorFilter = symbol.ColorFilter != null
            ? SKColorFilter.CreateColorMatrix(symbol.ColorFilter(context))
            : null;

        // Raster icons carry their decoded image in the native slot; vector
        // icons (mapsforge SVG symbols) are drawn as pictures instead
        var picture = (symbol.Icon as IVectorSprite)?.GetPicture();

        var image = picture == null ? symbol.Icon.GetImage() : null;

        var imgWidth = picture != null ? symbol.Icon.Width / symbol.Icon.PixelRatio : image!.Width;
        var imgHeight = picture != null ? symbol.Icon.Height / symbol.Icon.PixelRatio : image!.Height;
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

        if (picture != null)
        {
            // Vector icons are drawn from their picture, scaled from the
            // intrinsic units of the SVG to the pixel size of the sprite
            var cullRect = picture.CullRect;

            canvas.Save();
            canvas.Translate(-anchorX * imgWidth + offsetX * symbol.Icon.PixelRatio,
                -anchorY * imgHeight + offsetY * symbol.Icon.PixelRatio);
            canvas.Scale(imgWidth / cullRect.Width, imgHeight / cullRect.Height);
            canvas.DrawPicture(picture, paint);
            canvas.Restore();
        }
        else
        {
            canvas.DrawImage(image!,
                -anchorX * imgWidth + offsetX * symbol.Icon.PixelRatio,
                -anchorY * imgHeight + offsetY * symbol.Icon.PixelRatio,
                ScreenSampling,
                paint);
        }

        canvas.Restore();

        if (showValidBorders)
        {
            // The icon is drawn 1:1 in real screen space at the given position,
            // so its envelope frames the drawn icon exactly
            canvas.DrawRect(
                new SKRect((float)(symbol.Envelope!.MinX + screenX), (float)(symbol.Envelope!.MinY + screenY),
                (float)(symbol.Envelope!.MaxX + screenX), (float)(symbol.Envelope!.MaxY + screenY)), DebugPaint);
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

        // Save for later use
        var screenEnvelope = symbol.Envelope.Copy();

        // The icon is centered on the anchor: the whole icon envelope must fit
        // on the path around it (MapLibre requires the label to fit before the
        // beginning and after the end of the line)
        using (var pathMeasure = new SKPathMeasure(path))
        {
            if (startPos + (float)symbol.Envelope.MinX < 0 ||
                startPos + (float)symbol.Envelope.MaxX > pathMeasure.Length)
            {
                return SymbolPlacementStatus.Collision;
            }
        }

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

        // The icon is drawn at CSS pixel dimensions (atlas pixels divided by the pixel ratio),
        // scaled by the icon size
        var width = (int)Math.Ceiling(symbol.Icon.Width / symbol.Icon.PixelRatio * symbol.Scale);
        var height = (int)Math.Ceiling(symbol.Icon.Height / symbol.Icon.PixelRatio * symbol.Scale);

        // Vector sprites draw from their picture (parsed lazily on draw), no
        // atlas cutout is created for them
        if (symbol.Icon.Native is null && symbol.Icon is not IVectorSprite)
        {
            symbol.Icon.Atlas.Native ??= SKImage.FromEncodedData(symbol.Icon.Atlas.Binary);

            // Cut out the sprite 1:1 in atlas pixels. Never bake the scale into the cached
            // image: the sprite is shared between symbols with different icon sizes, and
            // DrawIcon applies pixel ratio and scale when drawing.
            var atlasImage = (SKImage)symbol.Icon.Atlas.Native;
            var spriteRect = new SKRect(symbol.Icon.X, symbol.Icon.Y,
                symbol.Icon.X + symbol.Icon.Width,
                symbol.Icon.Y + symbol.Icon.Height);

            using (var spriteBitmap = new SKBitmap(symbol.Icon.Width, symbol.Icon.Height,
                       atlasImage.ColorType, atlasImage.AlphaType))
            using (var spriteCanvas = new SKCanvas(spriteBitmap))
            {
                spriteCanvas.DrawImage(atlasImage, spriteRect,
                    new SKRect(0, 0, symbol.Icon.Width, symbol.Icon.Height)
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
