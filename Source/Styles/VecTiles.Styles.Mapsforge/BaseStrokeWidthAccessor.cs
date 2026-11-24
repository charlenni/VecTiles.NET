using VecTiles.Common.Interfaces;

namespace VecTiles.Styles.Mapsforge;

/// <summary>
/// Gets the base-stroke-width factor of the render theme a layer belongs to.
/// Theme independent code (paint creation) uses it to scale stroke widths.
/// </summary>
internal static class BaseStrokeWidthAccessor
{
    /// <summary>
    /// Gets the base-stroke-width factor of the layer's theme.
    /// </summary>
    /// <param name="style">The layer style.</param>
    /// <returns>The factor, or null when the style is not a Mapsforge layer.</returns>
    public static float? TryGet(ILayerStyle style)
    {
        return style is MFLayerStyle mapsforgeStyle ? mapsforgeStyle.BaseStrokeWidth : null;
    }
}

/// <summary>
/// Gets the base-text-size factor of the render theme a layer belongs to.
/// Theme independent code uses it to scale text sizes.
/// </summary>
internal static class BaseTextSizeAccessor
{
    /// <summary>
    /// Gets the base-text-size factor of the layer's theme.
    /// </summary>
    /// <param name="style">The layer style.</param>
    /// <returns>The factor, or null when the style is not a Mapsforge layer.</returns>
    public static float? TryGet(ILayerStyle style)
    {
        return style is MFLayerStyle mapsforgeStyle ? mapsforgeStyle.BaseTextSize : null;
    }
}
