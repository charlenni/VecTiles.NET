using NetTopologySuite.Geometries;
using SkiaSharp;

namespace VecTiles.Renderers.Skia.Extensions;

public static class PointExtensions
{
    public static SKPoint ToSKPoint(this Point point)
    {
        return new SKPoint((float)point.X, (float)point.Y);
    }
}