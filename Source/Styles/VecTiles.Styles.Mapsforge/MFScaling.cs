using VecTiles.Common.Primitives;

namespace VecTiles.Styles.Mapsforge;

/// <summary>
/// Scale conversions between Mapsforge render theme units and the tile space
/// of the renderer: a Mapsforge theme is defined relative to a tile of
/// 256 px, while the renderer draws tiles of 512 px, so every px value of
/// the theme (stroke widths, dash arrays, radii, font sizes, symbol sizes,
/// offsets, gaps) scales by two when it leaves the style factories.
/// </summary>
internal static class MFScaling
{
    /// <summary>
    /// Factor from Mapsforge theme units (256 px tiles) to the units of the
    /// renderer (512 px tiles).
    /// </summary>
    internal const float TileScale = 2f;

    /// <summary>
    /// Gets the zoom dependent stroke scale of the original Mapsforge
    /// renderer: after zoom level 12 every stroke grows by a factor of 1.5
    /// per zoom level (RenderContext.STROKE_INCREASE and
    /// STROKE_MIN_ZOOM_LEVEL), below it stays constant. Applies to stroke
    /// widths, dy offsets and font sizes, matching the original tile
    /// rendering. Contexts without a zoom use the base scale.
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
