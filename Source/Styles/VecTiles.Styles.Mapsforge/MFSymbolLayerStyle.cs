using VecTiles.Common.Enums;
using VecTiles.Styles.Mapsforge.Enums;
using VecTiles.Styles.Mapsforge.Expressions;

namespace VecTiles.Styles.Mapsforge;

/// <summary>
/// Point symbol layer of a Mapsforge render theme, created from the symbol
/// drawing instruction of a rule: an icon drawn at the feature (for nodes at
/// the node position, for ways at the centroid of the geometry).
/// </summary>
public class MFSymbolLayerStyle : MFLayerStyle
{
    /// <summary>
    /// Initializes the point symbol layer.
    /// </summary>
    /// <param name="name">Name of the layer (unique within the render theme).</param>
    /// <param name="filter">Filter expression of the combined rule chain.</param>
    /// <param name="zoomMin">Minimal zoom level of the rule chain (inclusive).</param>
    /// <param name="zoomMax">Maximal zoom level of the rule chain (inclusive).</param>
    /// <param name="src">Source of the icon.</param>
    /// <param name="symbolWidth">Requested icon width in px, or null for the intrinsic width.</param>
    /// <param name="symbolHeight">Requested icon height in px, or null for the intrinsic height.</param>
    /// <param name="symbolPercent">Requested icon size in percent of the intrinsic size, or null.</param>
    /// <param name="position">Position of the icon relative to the feature point.</param>
    /// <param name="priority">Priority of the symbol in the placement (higher priority first).</param>
    /// <param name="display">Display directive of the symbol.</param>
    /// <param name="cat">Category of the rule for the style menu.</param>
    /// <param name="id">Id of the symbol, referenced by captions ("symbol-id").</param>
    public MFSymbolLayerStyle(string name, MFAllExpression filter, int zoomMin, int zoomMax,
        string src, int? symbolWidth, int? symbolHeight, int? symbolPercent,
        MapsforgePosition position, int priority, MapsforgeDisplay display, string cat,
        string? id = null)
        : base(name, LayerStyleType.Symbol, filter, zoomMin, zoomMax)
    {
        Src = src;
        SymbolWidth = symbolWidth;
        SymbolHeight = symbolHeight;
        SymbolPercent = symbolPercent;
        Position = position;
        Priority = priority;
        Display = display;
        Cat = cat;
        Id = id;
    }

    /// <summary>
    /// Gets the source of the icon (attribute src).
    /// </summary>
    public string Src { get; }

    /// <summary>
    /// Gets the requested icon width in px (attribute symbol-width), or null.
    /// </summary>
    public int? SymbolWidth { get; }

    /// <summary>
    /// Gets the requested icon height in px (attribute symbol-height), or null.
    /// </summary>
    public int? SymbolHeight { get; }

    /// <summary>
    /// Gets the requested icon size in percent of the intrinsic size (attribute
    /// symbol-percent), or null.
    /// </summary>
    public int? SymbolPercent { get; }

    /// <summary>
    /// Gets the position of the icon relative to the feature point (attribute position).
    /// </summary>
    public MapsforgePosition Position { get; }

    /// <summary>
    /// Gets the priority of the symbol in the placement (attribute priority).
    /// </summary>
    public int Priority { get; }

    /// <summary>
    /// Gets the display directive (attribute display).
    /// </summary>
    public MapsforgeDisplay Display { get; }

    /// <summary>
    /// Gets the id of the symbol (attribute id), referenced by captions with
    /// symbol-id, or null when the symbol has no id.
    /// </summary>
    public string? Id { get; }
}
