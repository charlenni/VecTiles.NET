using VecTiles.Common.Interfaces;

namespace VecTiles.Common.Primitives;

/// <summary>
/// Class holding information about bitmap regions data (sprites) in Json format
/// </summary>
public class Sprite : ISprite
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Sprite"/> class from a region within an atlas bitmap.
    /// </summary>
    /// <param name="atlas">The atlas bitmap containing the sprite.</param>
    /// <param name="x">The X-coordinate of the sprite's top-left corner within the atlas.</param>
    /// <param name="y">The Y-coordinate of the sprite's top-left corner within the atlas.</param>
    /// <param name="width">The width of the sprite in pixels.</param>
    /// <param name="height">The height of the sprite in pixels.</param>
    /// <param name="pixelRatio">The pixel ratio of the sprite. Defaults to 1.0.</param>
    public Sprite(IBitmap atlas, int x, int y, int width, int height, float pixelRatio = 1.0f)
    {
        Atlas = atlas;
        X = x;
        Y = y;
        Width = width;
        Height = height;
        PixelRatio = pixelRatio;
    }

    /// <inheritdoc/>
    public IBitmap Atlas { get; set; }

    /// <inheritdoc/>
    public int X { get; set; }

    /// <inheritdoc/>
    public int Y { get; set; }

    /// <inheritdoc/>
    public int Width { get; set; }

    /// <inheritdoc/>
    public int Height { get; set; }

    /// <summary>
    /// Store for native bitmap region object.
    /// </summary>
    public object? Native { get; set; }

    /// <inheritdoc/>
    public float PixelRatio { get; set; }

    /// <summary>
    /// Gets or sets the content area of the sprite as [x, y, width, height] used for symbol placement.
    /// </summary>
    public IList<float> Content { get; set; } = [];

    /// <summary>
    /// Gets or sets the horizontal stretchable pixel positions of a 9-sliced sprite.
    /// </summary>
    public IList<float> StrechX { get; set; } = [];

    /// <summary>
    /// Gets or sets the vertical stretchable pixel positions of a 9-sliced sprite.
    /// </summary>
    public IList<float> StrechY { get; set; } = [];
}
