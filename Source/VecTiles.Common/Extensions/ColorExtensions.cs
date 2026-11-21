using VecTiles.Common.Primitives;

namespace VecTiles.Common.Extensions;

/// <summary>
/// Provides extension methods for the <see cref="Color"/> struct.
/// </summary>
public static class ColorExtensions
{
    /// <summary>
    /// Returns a new <see cref="Color"/> with the alpha channel replaced by the given value.
    /// </summary>
    /// <param name="color">The color to modify.</param>
    /// <param name="alpha">The new alpha value (0 to 255).</param>
    /// <returns>A color with the same RGB channels and the new alpha value.</returns>
    public static Color WithAlpha(this Color color, byte alpha)
    {
        return color with { A = alpha };
    }
}
