using VecTiles.Common.Enums;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Styles.Mapsforge.Expressions;

namespace VecTiles.Styles.Mapsforge;

/// <summary>
/// Base class of all style layers created from the drawing instructions of a
/// Mapsforge render theme. One render theme instruction (e.g. a line, area or
/// caption element of a rule) becomes one flat style layer; the conditions of
/// the rule chain leading to the instruction are combined into its filter
/// expression.
/// </summary>
public abstract class MFLayerStyle : ILayerStyle
{
    /// <summary>
    /// Initializes the layer style.
    /// </summary>
    /// <param name="name">Name of the layer (unique within the render theme).</param>
    /// <param name="styleType">Style type the layer renders as.</param>
    /// <param name="filter">Filter expression of the combined rule chain.</param>
    /// <param name="zoomMin">Minimal zoom level of the rule chain (inclusive).</param>
    /// <param name="zoomMax">Maximal zoom level of the rule chain (inclusive);
    /// stored as the exclusive upper bound <c>zoomMax + 1</c>.</param>
    protected MFLayerStyle(string name, LayerStyleType styleType, MFAllExpression filter, int zoomMin, int zoomMax)
    {
        Name = name;
        StyleType = styleType;
        Filter = filter;
        MinZoom = zoomMin;
        MaxZoom = zoomMax + 1;
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public LayerStyleType StyleType { get; }

    /// <inheritdoc />
    public IExpression Filter { get; }

    /// <inheritdoc />
    public float MinZoom { get; }

    /// <summary>
    /// Maximal zoom level the layer applies to (Mapsforge zoom-max is
    /// inclusive, the renderer treats the upper zoom bound as exclusive, so
    /// the stored value is zoom-max + 1).
    /// </summary>
    public float MaxZoom { get; }

    /// <inheritdoc />
    /// <remarks>
    /// Set to false for layers whose display directive is "never" (they are
    /// kept in the theme for completeness but excluded from rendering).
    /// </remarks>
    public bool Visible { get; init; } = true;

    /// <inheritdoc />
    public string Source { get; init; } = string.Empty;

    /// <inheritdoc />
    public string SourceLayer { get; init; } = "mapsforge";

    /// <summary>
    /// Gets or sets the base-stroke-width factor of the render theme
    /// (attribute base-stroke-width), scaling stroke widths of the layer.
    /// </summary>
    public float BaseStrokeWidth { get; set; } = 1f;

    /// <summary>
    /// Gets or sets the base-text-size factor of the render theme
    /// (attribute base-text-size), scaling sizes of text of the layer.
    /// </summary>
    public float BaseTextSize { get; set; } = 1f;

    /// <summary>
    /// Gets or sets the category of the rule the layer was created from
    /// ("cat" attribute), used by the style menu of the render theme to toggle
    /// layers.
    /// </summary>
    public string Cat { get; set; } = string.Empty;

    /// <inheritdoc />
    public void Update(EvaluationContext context)
    {
        // Mapsforge paints are constant per layer, there is nothing to update
    }
}
