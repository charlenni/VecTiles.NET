using SkiaSharp;
using VecTiles.Common.Interfaces;

namespace VecTiles.Renderers.Skia.Extensions;

/// <summary>
/// Extension methods to convert bitmaps to SkiaSharp images.
/// </summary>
public static class IBitmapExtensions
{
    /// <summary>
    /// Gets the bitmap as an <see cref="SKImage" />, decoding the image data on
    /// first use and caching it in <see cref="IBitmap.Native" />.
    /// </summary>
    /// <param name="bitmap">Bitmap to convert.</param>
    /// <returns>The decoded SkiaSharp image.</returns>
    public static SKImage ToSKImage(this IBitmap bitmap)
    {
        bitmap.Native ??= SKImage.FromEncodedData(bitmap.Binary);

        return ((SKImage) bitmap.Native);
    }
}