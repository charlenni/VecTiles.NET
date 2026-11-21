using NetTopologySuite.Geometries;
using SkiaSharp;

namespace VecTiles.Renderers.Skia.Extensions;

/// <summary>
/// Extension methods to convert NTS points to SkiaSharp points.
/// </summary>
public static class PointExtensions
{
    /// <summary>
    /// Converts the point to an <see cref="SKPoint" />.
    /// </summary>
    /// <param name="point">Point to convert.</param>
    /// <returns>The equivalent SkiaSharp point.</returns>
    public static SKPoint ToSKPoint(this Point point)
    {
        return new SKPoint((float)point.X, (float)point.Y);
    }
}