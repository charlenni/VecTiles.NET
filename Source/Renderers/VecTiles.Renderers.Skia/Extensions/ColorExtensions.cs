using SkiaSharp;
using VecTiles.Common.Primitives;

namespace VecTiles.Renderers.Skia.Extensions;

/// <summary>
/// Extension methods to convert <see cref="Color" /> values to SkiaSharp colors.
/// </summary>
public static class ColorExtensions
{
    /// <summary>
    /// Converts the color to an <see cref="SKColor" />.
    /// </summary>
    /// <param name="color">Color to convert.</param>
    /// <returns>The equivalent SkiaSharp color.</returns>
    public static SKColor ToSKColor(this Color color)
    {
        return new SKColor(color.R, color.G, color.B, color.A);
    }
}
