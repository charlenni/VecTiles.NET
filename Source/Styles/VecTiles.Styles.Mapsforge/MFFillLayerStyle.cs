using VecTiles.Common.Enums;
using VecTiles.Common.Primitives;
using VecTiles.Styles.Mapsforge.Expressions;

namespace VecTiles.Styles.Mapsforge;

/// <summary>
/// Area fill layer of a Mapsforge render theme, created from the area drawing
/// instruction of a rule. Areas with a pattern (src attribute) carry the
/// pattern source in <see cref="PatternSrc" /> and get a fill paint without
/// color instead.
/// </summary>
public class MFFillLayerStyle : MFLayerStyle
{
    /// <summary>
    /// Initializes the fill layer.
    /// </summary>
    /// <param name="name">Name of the layer (unique within the render theme).</param>
    /// <param name="filter">Filter expression of the combined rule chain.</param>
    /// <param name="zoomMin">Minimal zoom level of the rule chain (inclusive).</param>
    /// <param name="zoomMax">Maximal zoom level of the rule chain (inclusive).</param>
    /// <param name="fill">Fill color of the area.</param>
    /// <param name="stroke">Stroke color of the area outline.</param>
    /// <param name="strokeWidth">Stroke width of the area outline.</param>
    /// <param name="patternSrc">Source of the area pattern, or null for a plain fill.</param>
    public MFFillLayerStyle(string name, MFAllExpression filter, int zoomMin, int zoomMax,
        Color fill, Color stroke, float strokeWidth, string? patternSrc)
        : base(name, LayerStyleType.Fill, filter, zoomMin, zoomMax)
    {
        Fill = fill;
        Stroke = stroke;
        StrokeWidth = strokeWidth;
        PatternSrc = patternSrc;
    }

    /// <summary>
    /// Gets the fill color (attribute fill).
    /// </summary>
    public Color Fill { get; }

    /// <summary>
    /// Gets the outline color (attribute stroke).
    /// </summary>
    public Color Stroke { get; }

    /// <summary>
    /// Gets the outline width (attribute stroke-width).
    /// </summary>
    public float StrokeWidth { get; }

    /// <summary>
    /// Gets the pattern source of the area (attribute src), or null when the
    /// area is a plain fill.
    /// </summary>
    public string? PatternSrc { get; }
}
