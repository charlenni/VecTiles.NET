using NetTopologySuite.Geometries;

namespace VecTiles.Common.Extensions;

/// <summary>
/// Provides extension methods for <see cref="float"/> arrays.
/// </summary>
public static class FloatExtensions
{
    /// <summary>
    /// Converts a two-element float array (an x/y offset) into a <see cref="Point"/>,
    /// multiplying both components by the given scale.
    /// </summary>
    /// <param name="offset">The array with the x and y offsets; must contain exactly two elements.</param>
    /// <param name="scale">The scale applied to both components. Defaults to 1.0.</param>
    /// <returns>A point with the scaled offsets, or <c>Point(0, 0)</c> if the array has any other length.</returns>
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
