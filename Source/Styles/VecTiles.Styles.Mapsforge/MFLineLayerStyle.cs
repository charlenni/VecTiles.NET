using VecTiles.Common.Enums;
using VecTiles.Common.Primitives;
using VecTiles.Styles.Mapsforge.Expressions;

namespace VecTiles.Styles.Mapsforge;

/// <summary>
/// Line layer of a Mapsforge render theme, created from the line drawing
/// instruction of a rule.
/// </summary>
public class MFLineLayerStyle : MFLayerStyle
{
    /// <summary>
    /// Initializes the line layer.
    /// </summary>
    /// <param name="name">Name of the layer (unique within the render theme).</param>
    /// <param name="filter">Filter expression of the combined rule chain.</param>
    /// <param name="zoomMin">Minimal zoom level of the rule chain (inclusive).</param>
    /// <param name="zoomMax">Maximal zoom level of the rule chain (inclusive).</param>
    /// <param name="stroke">Stroke color of the line.</param>
    /// <param name="strokeWidth">Stroke width of the line.</param>
    /// <param name="strokeDasharray">Dash lengths alternating between dash and gap, or null for a solid line.</param>
    /// <param name="cap">Stroke cap of the line.</param>
    /// <param name="join">Stroke join of the line.</param>
    /// <param name="patternSrc">Source of the line pattern, or null for a plain stroke.</param>
    /// <param name="dy">Vertical offset of the line in px.</param>
    public MFLineLayerStyle(string name, MFAllExpression filter, int zoomMin, int zoomMax,
        Color stroke, float strokeWidth, float[]? strokeDasharray, StrokeCap cap, StrokeJoin join,
        string? patternSrc, float dy)
        : base(name, LayerStyleType.Line, filter, zoomMin, zoomMax)
    {
        Stroke = stroke;
        StrokeWidth = strokeWidth;
        StrokeDasharray = strokeDasharray;
        Cap = cap;
        Join = join;
        PatternSrc = patternSrc;
        Dy = dy;
    }

    /// <summary>
    /// Gets the stroke color (attribute stroke).
    /// </summary>
    public Color Stroke { get; }

    /// <summary>
    /// Gets the stroke width (attribute stroke-width).
    /// </summary>
    public float StrokeWidth { get; }

    /// <summary>
    /// Gets the dash lengths (attribute stroke-dasharray), alternating between
    /// dash and gap, or null for a solid line.
    /// </summary>
    public float[]? StrokeDasharray { get; }

    /// <summary>
    /// Gets the stroke cap (attribute stroke-linecap).
    /// </summary>
    public StrokeCap Cap { get; }

    /// <summary>
    /// Gets the stroke join (attribute stroke-linejoin).
    /// </summary>
    public StrokeJoin Join { get; }

    /// <summary>
    /// Gets the pattern source of the line (attribute src), or null when the
    /// line is a plain stroke.
    /// </summary>
    public string? PatternSrc { get; }

    /// <summary>
    /// Gets the vertical offset of the line in px (attribute dy).
    /// </summary>
    public float Dy { get; }
}
