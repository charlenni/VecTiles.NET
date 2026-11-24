using VecTiles.Common.Primitives;
using VecTiles.Common.Enums;
using VecTiles.Styles.Mapsforge.Enums;
using VecTiles.Styles.Mapsforge.Expressions;

namespace VecTiles.Styles.Mapsforge;

/// <summary>
/// Caption layer of a Mapsforge render theme, created from the caption drawing
/// instruction of a rule: a text label shown at the feature (for nodes at the
/// node position, for ways at the centroid of the geometry).
/// </summary>
public class MFCaptionLayerStyle : MFLayerStyle
{
    /// <summary>
    /// Initializes the caption layer.
    /// </summary>
    /// <param name="name">Name of the layer (unique within the render theme).</param>
    /// <param name="filter">Filter expression of the combined rule chain.</param>
    /// <param name="zoomMin">Minimal zoom level of the rule chain (inclusive).</param>
    /// <param name="zoomMax">Maximal zoom level of the rule chain (inclusive).</param>
    /// <param name="key">Attribute key holding the text (attribute k).</param>
    /// <param name="fontFamily">Font family (attribute font-family).</param>
    /// <param name="fontStyle">Font style (attribute font-style).</param>
    /// <param name="fontSize">Font size in px (attribute font-size, scaled by base-text-size).</param>
    /// <param name="fill">Text color (attribute fill).</param>
    /// <param name="stroke">Text outline (halo) color (attribute stroke).</param>
    /// <param name="strokeWidth">Text outline (halo) width (attribute stroke-width).</param>
    /// <param name="dy">Vertical offset of the text relative to its position (attribute dy).</param>
    /// <param name="position">Position of the text relative to the referenced symbol (attribute position).</param>
    /// <param name="priority">Priority of the text in the placement (attribute priority).</param>
    /// <param name="display">Display directive of the text (attribute display).</param>
    /// <param name="symbolId">Id of the symbol the position refers to (attribute symbol-id), or null.</param>
    /// <param name="textTransform">Transformation applied to the text (attribute text-transform).</param>
    /// <param name="textWrapWidth">Wrapping width of the text (attribute text-wrap-width), or null.</param>
    public MFCaptionLayerStyle(string name, MFAllExpression filter, int zoomMin, int zoomMax,
        string key, MapsforgeFontFamily fontFamily, MapsforgeFontStyle fontStyle, float fontSize,
        Color fill, Color stroke, float strokeWidth, float dy, MapsforgePosition position,
        int priority, MapsforgeDisplay display, string? symbolId,
        MapsforgeTextTransform textTransform, int? textWrapWidth)
        : base(name, LayerStyleType.Symbol, filter, zoomMin, zoomMax)
    {
        Key = key;
        FontFamily = fontFamily;
        FontStyle = fontStyle;
        FontSize = fontSize;
        Fill = fill;
        Stroke = stroke;
        StrokeWidth = strokeWidth;
        Dy = dy;
        Position = position;
        Priority = priority;
        Display = display;
        SymbolId = symbolId;
        TextTransform = textTransform;
        TextWrapWidth = textWrapWidth;
    }

    /// <summary>
    /// Gets the attribute key holding the text (attribute k).
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Gets the font family (attribute font-family).
    /// </summary>
    public MapsforgeFontFamily FontFamily { get; }

    /// <summary>
    /// Gets the font style (attribute font-style).
    /// </summary>
    public MapsforgeFontStyle FontStyle { get; }

    /// <summary>
    /// Gets the font size in px (attribute font-size, scaled by base-text-size).
    /// </summary>
    public float FontSize { get; }

    /// <summary>
    /// Gets the text color (attribute fill).
    /// </summary>
    public Color Fill { get; }

    /// <summary>
    /// Gets the text outline (halo) color (attribute stroke).
    /// </summary>
    public Color Stroke { get; }

    /// <summary>
    /// Gets the text outline (halo) width (attribute stroke-width).
    /// </summary>
    public float StrokeWidth { get; }

    /// <summary>
    /// Gets the vertical offset of the text relative to its position (attribute dy).
    /// </summary>
    public float Dy { get; }

    /// <summary>
    /// Gets the position of the text relative to the referenced symbol (attribute position).
    /// </summary>
    public MapsforgePosition Position { get; }

    /// <summary>
    /// Gets the priority of the text in the placement (attribute priority).
    /// </summary>
    public int Priority { get; }

    /// <summary>
    /// Gets the display directive (attribute display).
    /// </summary>
    public MapsforgeDisplay Display { get; }

    /// <summary>
    /// Gets the id of the symbol the position refers to (attribute symbol-id),
    /// or null when the text is positioned at the feature point.
    /// </summary>
    public string? SymbolId { get; }

    /// <summary>
    /// Gets the transformation applied to the text (attribute text-transform).
    /// </summary>
    public MapsforgeTextTransform TextTransform { get; }

    /// <summary>
    /// Gets the wrapping width of the text (attribute text-wrap-width), or null
    /// when the text is not wrapped.
    /// </summary>
    public int? TextWrapWidth { get; }

    /// <summary>
    /// Gets the source of the icon the position refers to (resolved from the
    /// symbol layer with <see cref="SymbolId"/> at load time), or null when the
    /// text is not positioned relative to an icon.
    /// </summary>
    public string? PositionSymbolSrc { get; set; }

    /// <summary>
    /// Gets or sets the symbol layer the position refers to (resolved from
    /// <see cref="SymbolId"/> at load time, including its size attributes), or
    /// null when the text is not positioned relative to an icon.
    /// </summary>
    public MFSymbolLayerStyle? PositionSymbol { get; set; }
}
