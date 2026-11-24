using VecTiles.Common.Primitives;
using VecTiles.Common.Enums;
using VecTiles.Styles.Mapsforge.Enums;
using VecTiles.Styles.Mapsforge.Expressions;

namespace VecTiles.Styles.Mapsforge;

/// <summary>
/// Path text layer of a Mapsforge render theme, created from the pathText
/// drawing instruction of a rule: a text label repeated along a path (e.g.
/// street names along roads).
/// </summary>
public class MFPathTextLayerStyle : MFLayerStyle
{
    /// <summary>
    /// Initializes the path text layer.
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
    /// <param name="repeat">Whether the text is repeated along the path (attribute repeat).</param>
    /// <param name="repeatGap">Gap between text repeats in px (attribute repeat-gap).</param>
    /// <param name="repeatStart">Distance of the first text from the path start in px (attribute repeat-start).</param>
    /// <param name="rotate">Whether the text is rotated with the path direction (attribute rotate).</param>
    /// <param name="dy">Vertical offset of the text relative to the path (attribute dy).</param>
    /// <param name="priority">Priority of the text in the placement (attribute priority).</param>
    /// <param name="display">Display directive of the text (attribute display).</param>
    /// <param name="textOrientation">Orientation of the text (attribute text-orientation).</param>
    /// <param name="textTransform">Transformation applied to the text (attribute text-transform).</param>
    public MFPathTextLayerStyle(string name, MFAllExpression filter, int zoomMin, int zoomMax,
        string key, MapsforgeFontFamily fontFamily, MapsforgeFontStyle fontStyle, float fontSize,
        Color fill, Color stroke, float strokeWidth, bool repeat, float repeatGap, float repeatStart,
        bool rotate, float dy, int priority, MapsforgeDisplay display,
        string textOrientation, MapsforgeTextTransform textTransform)
        : base(name, LayerStyleType.Symbol, filter, zoomMin, zoomMax)
    {
        Key = key;
        FontFamily = fontFamily;
        FontStyle = fontStyle;
        FontSize = fontSize;
        Fill = fill;
        Stroke = stroke;
        StrokeWidth = strokeWidth;
        Repeat = repeat;
        RepeatGap = repeatGap;
        RepeatStart = repeatStart;
        Rotate = rotate;
        Dy = dy;
        Priority = priority;
        Display = display;
        TextOrientation = textOrientation;
        TextTransform = textTransform;
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
    /// Gets a value indicating whether the text is repeated along the path (attribute repeat).
    /// </summary>
    public bool Repeat { get; }

    /// <summary>
    /// Gets the gap between text repeats in px (attribute repeat-gap).
    /// </summary>
    public float RepeatGap { get; }

    /// <summary>
    /// Gets the distance of the first text from the path start in px (attribute repeat-start).
    /// </summary>
    public float RepeatStart { get; }

    /// <summary>
    /// Gets a value indicating whether the text is rotated with the path direction (attribute rotate).
    /// </summary>
    public bool Rotate { get; }

    /// <summary>
    /// Gets the vertical offset of the text relative to the path (attribute dy).
    /// </summary>
    public float Dy { get; }

    /// <summary>
    /// Gets the priority of the text in the placement (attribute priority).
    /// </summary>
    public int Priority { get; }

    /// <summary>
    /// Gets the display directive (attribute display).
    /// </summary>
    public MapsforgeDisplay Display { get; }

    /// <summary>
    /// Gets the orientation of the text (attribute text-orientation).
    /// </summary>
    public string TextOrientation { get; }

    /// <summary>
    /// Gets the transformation applied to the text (attribute text-transform).
    /// </summary>
    public MapsforgeTextTransform TextTransform { get; }
}
