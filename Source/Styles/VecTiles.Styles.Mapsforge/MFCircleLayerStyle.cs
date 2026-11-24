using VecTiles.Common.Enums;
using VecTiles.Common.Primitives;
using VecTiles.Styles.Mapsforge.Expressions;

namespace VecTiles.Styles.Mapsforge;

/// <summary>
/// Circle layer of a Mapsforge render theme, created from the circle drawing
/// instruction of a rule. Circles are drawn at point features.
/// </summary>
public class MFCircleLayerStyle : MFLayerStyle
{
    /// <summary>
    /// Initializes the circle layer.
    /// </summary>
    /// <param name="name">Name of the layer (unique within the render theme).</param>
    /// <param name="filter">Filter expression of the combined rule chain.</param>
    /// <param name="zoomMin">Minimal zoom level of the rule chain (inclusive).</param>
    /// <param name="zoomMax">Maximal zoom level of the rule chain (inclusive).</param>
    /// <param name="radius">Radius of the circle.</param>
    /// <param name="fill">Fill color of the circle.</param>
    /// <param name="stroke">Stroke color of the circle.</param>
    /// <param name="strokeWidth">Stroke width of the circle.</param>
    public MFCircleLayerStyle(string name, MFAllExpression filter, int zoomMin, int zoomMax,
        float radius, Color fill, Color stroke, float strokeWidth)
        : base(name, LayerStyleType.Circle, filter, zoomMin, zoomMax)
    {
        Radius = radius;
        Fill = fill;
        Stroke = stroke;
        StrokeWidth = strokeWidth;
    }

    /// <summary>
    /// Gets the radius of the circle (attribute radius).
    /// </summary>
    public float Radius { get; }

    /// <summary>
    /// Gets the fill color of the circle (attribute fill).
    /// </summary>
    public Color Fill { get; }

    /// <summary>
    /// Gets the stroke color of the circle (attribute stroke).
    /// </summary>
    public Color Stroke { get; }

    /// <summary>
    /// Gets the stroke width of the circle (attribute stroke-width).
    /// </summary>
    public float StrokeWidth { get; }
}
