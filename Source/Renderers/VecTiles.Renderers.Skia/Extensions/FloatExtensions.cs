using SkiaSharp;

namespace VecTiles.Renderers.Skia.Extensions;

/// <summary>
/// Extension methods for float arrays.
/// </summary>
public static class FloatExtensions
{
    /// <summary>
    /// Converts an offset array with x and y components to a scaled
    /// <see cref="SKPoint" />. Returns the origin when the array has no
    /// exactly two components.
    /// </summary>
    /// <param name="offset">Array with x and y offset components.</param>
    /// <param name="scale">Scale applied to both components.</param>
    /// <returns>The scaled point.</returns>
    public static SKPoint ToPoint(this float[] offset, float scale = 1.0f)
    {
        if (offset.Length == 2)
        {
            return new SKPoint(offset[0] * scale, offset[1] * scale);
        }
        else
        {
            return new SKPoint(0, 0);
        }
    }

}
