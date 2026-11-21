namespace VecTiles.Common.Interfaces;

/// <summary>
/// Represents a sprite: a rectangular region within a sprite atlas bitmap.
/// </summary>
public interface ISprite : IBitmapRegion
{
    /// <summary>
    /// Gets the atlas bitmap containing the sprite.
    /// </summary>
    IBitmap Atlas { get; }

    /// <summary>
    /// Gets the pixel ratio of the sprite, i.e. the (device) resolution relative to the style px size.
    /// </summary>
    float PixelRatio { get; }
}
