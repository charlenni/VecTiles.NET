namespace VecTiles.Controls.Mapsui.Extensions;

/// <summary>
/// Extension methods to convert between zoom levels and resolutions of
/// the GlobalSphericalMercator tile schema.
/// </summary>
public static class ZoomExtensions
{
    private const double ScaleFactor = 78271.51696401953125;

    private static readonly double[] Resolutions;

    static ZoomExtensions()
    {
        Resolutions = new double[24];

        for (int i = 0; i <= 23; i++)
            Resolutions[i] = ScaleFactor / (1 << i);
    }

    /// <summary>
    /// Converts a zoom level to its resolution.
    /// </summary>
    /// <param name="zoom">The zoom level to convert.</param>
    /// <returns>The resolution of the zoom level.</returns>
    public static double ToResolution(this double zoom)
    {
        return ScaleFactor / Math.Pow(2, zoom);
    }

    /// <summary>
    /// Converts a zoom level to its resolution.
    /// </summary>
    /// <param name="zoom">The zoom level to convert.</param>
    /// <returns>The resolution of the zoom level.</returns>
    public static double ToResolution(this float zoom)
    {
        return ToResolution((double)zoom);
    }

    /// <summary>
    /// Converts an integer zoom level to its resolution.
    /// </summary>
    /// <param name="level">The zoom level to convert.</param>
    /// <returns>The resolution of the zoom level.</returns>
    public static double ToResolution(this int level)
    {
        return ScaleFactor / (1 << level);
    }

    /// <summary>
    /// Converts a resolution to its zoom level.
    /// </summary>
    /// <param name="resolution">The resolution to convert.</param>
    /// <returns>The zoom level of the resolution.</returns>
    public static double ToZoomLevel(this double resolution)
    {
        return Math.Log(ScaleFactor / resolution, 2);
    }

    /// <summary>
    /// Converts a resolution to its zoom level.
    /// </summary>
    /// <param name="resolution">The resolution to convert.</param>
    /// <returns>The zoom level of the resolution.</returns>
    public static double ToZoomLevel(this float resolution)
    {
        return ToZoomLevel((double)resolution);
    }
}
