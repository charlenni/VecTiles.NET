using VecTiles.Common.Primitives;

namespace VecTiles.Styles.Mapsforge;

/// <summary>
/// Zoom mapping between Mapsforge render themes and the tile space of the
/// renderer: a 512 px tile of this renderer covers the same ground as a
/// 256 px tile of the Mapsforge tile grid at the same level (see
/// GlobalSphericalMercator512), so its pixel density corresponds to the
/// screen of the original renderer at OSM zoom level + 1 — e.g. the tiles
/// of level 19 hold the same number of pixels per ground unit as the
/// original screen at zoom 20. All px values of a theme therefore map 1:1
/// into the tile space, and the stroke growth of the original renderer
/// (RenderContext.STROKE_INCREASE = 1.5 per zoom level above
/// STROKE_MIN_ZOOM_LEVEL = 12) applies with the zoom level + 1.
/// </summary>
internal static class MFScaling
{
    /// <summary>
    /// Gets the zoom dependent stroke scale of the original Mapsforge
    /// renderer for the zoom level of the given evaluation context: above
    /// OSM zoom level 12 every stroke grows by a factor of 1.5 per zoom
    /// level, below it stays constant; contexts without a zoom use the base
    /// scale. The level is the zoom of the 512 px tiles of the renderer, so
    /// 1 is added to reach the OSM zoom level of the same pixel density.
    /// </summary>
    /// <param name="context">The evaluation context to scale for.</param>
    /// <returns>The stroke scale of the zoom level of the context.</returns>
    internal static float GetStrokeScale(EvaluationContext context)
    {
        return context.Zoom is float level
            ? MathF.Pow(1.5f, MathF.Max(level - 11f, 0f))
            : 1f;
    }
}
