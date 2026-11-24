using VecTiles.Common.Interfaces;

namespace VecTiles.Common.Primitives;

/// <summary>
/// A sprite that is defined by vector source data (e.g. an SVG document)
/// instead of a region of raster pixels. The renderer parses the source data
/// lazily into its native vector object (e.g. an <c>SKPicture</c>), which is
/// held in the <see cref="ISprite.Native"/> slot; raster sprites keep a decoded
/// image there instead.
/// </summary>
public class VectorSprite : IVectorSprite
{
    private readonly Bitmap _source;

    /// <summary>
    /// Initializes a new vector sprite from its source data.
    /// </summary>
    /// <param name="vectorBinary">The bytes of the vector source data
    /// (e.g. the UTF-8 encoded SVG document).</param>
    /// <param name="width">Width the sprite is drawn at, in style px.</param>
    /// <param name="height">Height the sprite is drawn at, in style px.</param>
    /// <param name="intrinsicWidth">Intrinsic width of the vector graphic in
    /// source units; defaults to <paramref name="width"/>.</param>
    /// <param name="intrinsicHeight">Intrinsic height of the vector graphic in
    /// source units; defaults to <paramref name="height"/>.</param>
    /// <param name="pixelRatio">Pixel ratio of the sprite; defaults to 1.</param>
    public VectorSprite(byte[] vectorBinary,
        float width,
        float height,
        float? intrinsicWidth = null,
        float? intrinsicHeight = null,
        float pixelRatio = 1.0f)
    {
        _source = new Bitmap(vectorBinary);
        Width = (int)MathF.Ceiling(width);
        Height = (int)MathF.Ceiling(height);
        IntrinsicWidth = intrinsicWidth ?? width;
        IntrinsicHeight = intrinsicHeight ?? height;
        PixelRatio = pixelRatio;
    }

    /// <inheritdoc />
    public IBitmap Atlas => _source;

    /// <inheritdoc />
    public int X => 0;

    /// <inheritdoc />
    public int Y => 0;

    /// <inheritdoc />
    public int Width { get; }

    /// <inheritdoc />
    public int Height { get; }

    /// <inheritdoc />
    public object? Native { get; set; }

    /// <inheritdoc />
    public float PixelRatio { get; }

    /// <inheritdoc />
    public byte[] VectorBinary => _source.Binary;

    /// <inheritdoc />
    public float IntrinsicWidth { get; }

    /// <inheritdoc />
    public float IntrinsicHeight { get; }
}
