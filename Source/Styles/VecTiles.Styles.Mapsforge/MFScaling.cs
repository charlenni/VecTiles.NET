using VecTiles.Common.Primitives;

namespace VecTiles.Styles.Mapsforge;

/// <summary>
/// Zoom mapping between Mapsforge render themes and the tile space of the
/// renderer: the 512 px tiles of this renderer cover the tile grid of the
/// Mapsforge map files (a tile of a level covers the same ground as a 256 px
/// tile of the same level, see GlobalSphericalMercator512), so all px values
/// of a theme map 1:1 into our tile space, and the stroke growth of the
/// original renderer applies with the level directly: the original renderer
/// (RenderContext.STROKE_INCREASE = 1.5 per zoom level above
/// STROKE_MIN_ZOOM_LEVEL = 12) draws the strokes of a tile with the theme px
/// grown by 1.5 per tile level, and the same values are used when drawing
/// the same tile at twice its pixel size.
/// </summary>
internal static class MFScaling
{
    /// <summary>
    /// Gets the zoom dependent stroke scale of the original Mapsforge
    /// renderer for the zoom level of the given evaluation context: above
    /// zoom level 12 every stroke grows by a factor of 1.5 per zoom level,
    /// below it stays constant; contexts without a zoom use the base scale.
    /// Other theme px values (font sizes, icon sizes, dash arrays) do not
    /// grow with the zoom, matching the original renderer.
    /// </summary>
    /// <param name="context">The evaluation context to scale for.</param>
    /// <returns>The stroke scale of the zoom level of the context.</returns>
    internal static float GetStrokeScale(EvaluationContext context)
    {
        return context.Zoom is float level
            ? MathF.Pow(1.5f, MathF.Max(level - 12f, 0f))
            : 1f;
    }
}
