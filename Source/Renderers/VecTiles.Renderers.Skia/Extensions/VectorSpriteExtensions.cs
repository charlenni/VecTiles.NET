using System.Runtime.CompilerServices;
using SkiaSharp;
using Svg.Skia;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;

namespace VecTiles.Renderers.Skia.Extensions;

/// <summary>
/// Native drawing objects of vector sprites in the Skia renderer: vector
/// sprites (mapsforge SVG icons and patterns) hold an <see cref="SKPicture"/>
/// as their native object, raster sprites keep an <see cref="SKImage"/>.
/// </summary>
public static class VectorSpriteExtensions
{
    /// <summary>
    /// Gets the parsed SVG data of a vector sprite as an SKPicture,
    /// parsing it on first need and caching it in the native slot.
    /// </summary>
    /// <param name="sprite">The vector sprite holding encoded SVG data.</param>
    /// <returns>The picture, or null when the source cannot be parsed.</returns>
    public static SKPicture? GetPicture(this IVectorSprite sprite)
    {
        if (sprite.Native is SKPicture picture)
        {
            return picture;
        }

        if (sprite.Native != null)
        {
            // Native holds an object of another type (e.g. a rasterized
            // image), so the sprite cannot provide a picture
            return null;
        }

        try
        {
            using var stream = new MemoryStream(sprite.VectorBinary, writable: false);

            picture = new SKSvg().Load(stream);
        }
        catch (Exception)
        {
            // A broken icon must not prevent the theme from rendering
            picture = null;
        }

        sprite.Native = picture;

        return picture;
    }

    /// <summary>
    /// Gets the vector sprite rasterized at its display size, for draw paths
    /// that need an image (shader texture samplers). The rasterized image is
    /// cached per sprite.
    /// </summary>
    /// <param name="sprite">The vector sprite to rasterize.</param>
    /// <returns>The rasterized image.</returns>
    public static SKImage GetRasterImage(this IVectorSprite sprite)
    {
        return RasterImages.GetValue(sprite, static vectorSprite =>
        {
            var width = Math.Max(1, vectorSprite.Width);
            var height = Math.Max(1, vectorSprite.Height);

            var image = default(SKImage);

            using (var surface = SKSurface.Create(new SKImageInfo(width, height)))
            {
                surface.Canvas.Clear(SKColors.Transparent);

                var picture = vectorSprite.GetPicture();

                if (picture != null)
                {
                    // The picture is drawn in its intrinsic units, scaled to
                    // the display size of the sprite
                    var cullRect = picture.CullRect;

                    surface.Canvas.Scale(
                        width / cullRect.Width,
                        height / cullRect.Height);
                    surface.Canvas.DrawPicture(picture);
                }

                image = surface.Snapshot();
            }

            return image!;
        });
    }

    /// <summary>
    /// Rasterized vector sprites, kept per sprite (weakly referenced), so
    /// sprites that are shared between layers are rasterized only once.
    /// </summary>
    private static readonly ConditionalWeakTable<IVectorSprite, SKImage> RasterImages = new();

    /// <summary>
    /// Gets the native draw object of a sprite as an image for draw paths that
    /// need one: raster sprites return their decoded image (native slot),
    /// vector sprites return their rasterized image.
    /// </summary>
    /// <param name="sprite">The sprite to get an image of.</param>
    /// <returns>The image, or null when the sprite cannot be drawn.</returns>
    public static SKImage? GetImage(this ISprite sprite)
    {
        if (sprite is IVectorSprite vectorSprite)
        {
            return vectorSprite.GetRasterImage();
        }

        sprite.Atlas.Native ??= SKImage.FromEncodedData(sprite.Atlas.Binary);
        sprite.Native ??= ((SKImage)sprite.Atlas.Native).Subset(new SKRectI(
            sprite.X, sprite.Y, sprite.X + sprite.Width, sprite.Y + sprite.Height));

        return sprite.Native as SKImage;
    }
}
