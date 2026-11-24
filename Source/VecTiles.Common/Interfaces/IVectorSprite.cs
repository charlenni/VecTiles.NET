namespace VecTiles.Common.Interfaces;

/// <summary>
/// Marks a sprite whose content is vector data (e.g. an SVG document) instead
/// of raster bitmap pixels: <see cref="IBitmap.Binary"/> carries the vector
/// source data and the renderer keeps its platform representation (e.g. a
/// vector picture) in the <see cref="IBitmapRegion.Native"/> slot, in contrast
/// to raster sprites, whose <see cref="IBitmapRegion.Native"/> holds a decoded
/// image. Renderers decide by the type of the sprite (or of its native object)
/// how the icon is drawn; the sprite itself does not know its origin.
/// </summary>
public interface IVectorSprite : ISprite
{
    /// <summary>
    /// Gets the vector source data of the sprite (e.g. the bytes of an SVG
    /// document).
    /// </summary>
    byte[] VectorBinary { get; }

    /// <summary>
    /// Gets the intrinsic width of the vector graphic in source units
    /// (e.g. the width of the SVG viewBox or of its width attribute).
    /// </summary>
    float IntrinsicWidth { get; }

    /// <summary>
    /// Gets the intrinsic height of the vector graphic in source units
    /// (e.g. the height of the SVG viewBox or of its height attribute).
    /// </summary>
    float IntrinsicHeight { get; }
}
