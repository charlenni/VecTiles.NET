using NetTopologySuite.Geometries;
using VecTiles.Common.Enums;

namespace VecTiles.Common.Primitives;

public class TextPointSymbol : Symbol
{
    public TextPointSymbol(Tile tile, Point point, string text) : base(tile)
    {
        Point = point;
        Text = text;
    }

    /// <summary>
    /// Point where symbol is placed in world coordinates
    /// </summary>
    public Point Point { get; }

    /// <summary>
    /// Text block for this symbol
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// Is symbol optional?
    /// </summary>
    public bool Optional { get; internal set; }

    /// <summary>
    /// Allow other symbols to overlap this symbol
    /// </summary>
    public bool AllowOverlap { get; internal set; }

    /// <summary>
    /// Rotation of symbol in degrees
    /// </summary>
    public float Rotation { get; internal set; }

    /// <summary>
    /// Keep the text upright, so that is easier to read
    /// </summary>
    public bool KeepUpright { get; internal set; }

    /// <summary>
    /// Anchor of symbol given as relative position with [0..1, 0..1]
    /// </summary>
    public Point Anchor { get; internal set; } = new(0, 0);

    /// <summary>
    /// Offset from point in pixels
    /// </summary>
    public Point Offset { get; internal set; } = new(0, 0);

    /// <summary>
    /// Function to calculate color to use when drawing symbol from EvaluationContext
    /// </summary>
    public Func<EvaluationContext, Color>? Color { get; internal set; }

    /// <summary>
    /// Function to calculate opacity of symbol from EvaluationContext
    /// </summary>
    public Func<EvaluationContext, float>? Opacity { get; internal set; }

    /// <summary>
    /// Function to calculate halo color to use when drawing symbol from EvaluationContext
    /// </summary>
    public Func<EvaluationContext, Color>? HaloColor { get; internal set; }

    /// <summary>
    /// Function to calculate halo blur to use when drawing symbol from EvaluationContext
    /// </summary>
    public Func<EvaluationContext, float>? HaloBlur { get; internal set; }

    /// <summary>
    /// Function to calculate halo width to use when drawing symbol from EvaluationContext
    /// </summary>
    public Func<EvaluationContext, float>? HaloWidth { get; internal set; }

    /// <summary>
    /// Function to calculate translate of symbols point from EvaluationContext in pixels
    /// </summary>
    public Func<EvaluationContext, Point>? Translate { get; internal set; }

    /// <summary>
    /// Function to calculate anchor of translate (map or viewport) from EvaluationContext
    /// </summary>
    public Func<EvaluationContext, MapAlignment>? TranslateAnchor { get; internal set; }

    /// <summary>
    /// Maximum width for the text. If the text exceeds the max width, it will be wrapped.
    /// </summary>
    public Func<EvaluationContext, float, float>? MaxWidth { get; internal set; }
}
