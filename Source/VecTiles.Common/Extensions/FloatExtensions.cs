using NetTopologySuite.Geometries;

namespace VecTiles.Common.Extensions;

public static class FloatExtensions
{
    public static Point ToPoint(this float[] offset, float scale = 1.0f)
    {
        if (offset.Length == 2)
        {
            return new Point(offset[0] * scale, offset[1] * scale);
        }
        else
        {
            return new Point(0, 0);
        }
    }

}
