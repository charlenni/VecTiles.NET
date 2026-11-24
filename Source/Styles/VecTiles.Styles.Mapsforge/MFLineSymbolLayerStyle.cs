using VecTiles.Common.Enums;
using VecTiles.Styles.Mapsforge.Enums;
using VecTiles.Styles.Mapsforge.Expressions;

namespace VecTiles.Styles.Mapsforge;

/// <summary>
/// Line symbol layer of a Mapsforge render theme, created from the lineSymbol
/// drawing instruction of a rule: an icon repeated along a line (e.g. oneway
/// arrows on roads).
/// </summary>
public class MFLineSymbolLayerStyle : MFLayerStyle
{
    /// <summary>
    /// Initializes the line symbol layer.
    /// </summary>
    /// <param name="name">Name of the layer (unique within the render theme).</param>
    /// <param name="filter">Filter expression of the combined rule chain.</param>
    /// <param name="zoomMin">Minimal zoom level of the rule chain (inclusive).</param>
    /// <param name="zoomMax">Maximal zoom level of the rule chain (inclusive).</param>
    /// <param name="src">Source of the icon (attribute src).</param>
    /// <param name="symbolWidth">Requested icon width in px, or null for the intrinsic width.</param>
    /// <param name="symbolHeight">Requested icon height in px, or null for the intrinsic height.</param>
    /// <param name="symbolPercent">Requested icon size in percent of the intrinsic size, or null.</param>
    /// <param name="repeat">Whether the icon is repeated along the line (attribute repeat).</param>
    /// <param name="repeatGap">Gap between icon repeats in px (attribute repeat-gap).</param>
    /// <param name="repeatStart">Distance of the first icon from the line start in px (attribute repeat-start).</param>
    /// <param name="rotate">Whether the icon is rotated with the line direction (attribute rotate).</param>
    /// <param name="orientation">Orientation of the icon (attribute symbol-orientation).</param>
    /// <param name="dy">Vertical offset of the icon relative to the line (attribute dy).</param>
    /// <param name="position">Position of the icon relative to the line (attribute position).</param>
    /// <param name="priority">Priority of the symbol in the placement (attribute priority).</param>
    /// <param name="display">Display directive of the symbol (attribute display).</param>
    public MFLineSymbolLayerStyle(string name, MFAllExpression filter, int zoomMin, int zoomMax,
        string src, int? symbolWidth, int? symbolHeight, int? symbolPercent,
        bool repeat, float repeatGap, float repeatStart, bool rotate, string orientation,
        float dy, MapsforgePosition position, int priority, MapsforgeDisplay display)
        : base(name, LayerStyleType.Symbol, filter, zoomMin, zoomMax)
    {
        Src = src;
        SymbolWidth = symbolWidth;
        SymbolHeight = symbolHeight;
        SymbolPercent = symbolPercent;
        Repeat = repeat;
        RepeatGap = repeatGap;
        RepeatStart = repeatStart;
        Rotate = rotate;
        Orientation = orientation;
        Dy = dy;
        Position = position;
        Priority = priority;
        Display = display;
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
    /// Gets a value indicating whether the icon is repeated along the line (attribute repeat).
    /// </summary>
    public bool Repeat { get; }

    /// <summary>
    /// Gets the gap between icon repeats in px (attribute repeat-gap).
    /// </summary>
    public float RepeatGap { get; }

    /// <summary>
    /// Gets the distance of the first icon from the line start in px (attribute repeat-start).
    /// </summary>
    public float RepeatStart { get; }

    /// <summary>
    /// Gets a value indicating whether the icon is rotated with the line direction (attribute rotate).
    /// </summary>
    public bool Rotate { get; }

    /// <summary>
    /// Gets the orientation of the icon (attribute symbol-orientation).
    /// </summary>
    public string Orientation { get; }

    /// <summary>
    /// Gets the vertical offset of the icon relative to the line (attribute dy).
    /// </summary>
    public float Dy { get; }

    /// <summary>
    /// Gets the position of the icon relative to the line (attribute position).
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
}
