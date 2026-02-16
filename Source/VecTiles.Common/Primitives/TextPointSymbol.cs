using NetTopologySuite.Geometries;
using VecTiles.Common.Enums;

namespace VecTiles.Common.Primitives;

public class TextPointSymbol : Symbol
{
    public TextPointSymbol(Tile tile, ulong id, Point point, string text) : base(tile, id)
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
    public bool Optional { get; init; }

    /// <summary>
    /// Allow other symbols to overlap this symbol
    /// </summary>
    public bool AllowOverlap { get; init; }

    /// <summary>
    /// Rotation of symbol in degrees
    /// </summary>
    public float Rotation { get; init; }

    /// <summary>
    /// Rotation alignment (map or viewport) for rotation
    /// </summary>
    public MapAlignment RotationAlignment { get; init; }

    /// <summary>
    /// Padding around symbol in pixel
    /// </summary>
    public int Padding { get; init; }

    /// <summary>
    /// Keep the text upright, so that is easier to read
    /// </summary>
    public bool KeepUpright { get; init; }

    /// <summary>
    /// Anchor of symbol given as relative position with [0..1, 0..1]
    /// </summary>
    public Point Anchor { get; init; } = new(0, 0);

    /// <summary>
    /// Offset from point in pixels
    /// </summary>
    public Point Offset { get; init; } = new(0, 0);

    /// <summary>
    /// Function to calculate color to use when drawing symbol from EvaluationContext
    /// </summary>
    public Func<EvaluationContext, Color>? Color { get; init; }

    /// <summary>
    /// Function to calculate opacity of symbol from EvaluationContext
    /// </summary>
    public Func<EvaluationContext, float>? Opacity { get; init; }

    /// <summary>
    /// Text alignment in MaxWidth
    /// </summary>
    public TextJustify Alignment { get; init; }
    
    /// <summary>
    /// Text direction (LTR, RTL, Auto)
    /// </summary>
    public TextDirection Direction { get; init; }
    
    /// <summary>
    /// Font names
    /// </summary>
    public string[] FontNames { get; init; }

    /// <summary>
    /// Function to calculate font size of symbol text from EvaluationContext
    /// </summary>
    public Func<EvaluationContext, float>? FontSize { get; init; }

    /// <summary>
    /// Function to calculate halo color to use when drawing symbol from EvaluationContext
    /// </summary>
    public Func<EvaluationContext, Color>? HaloColor { get; init; }

    /// <summary>
    /// Function to calculate halo blur to use when drawing symbol from EvaluationContext
    /// </summary>
    public Func<EvaluationContext, float>? HaloBlur { get; init; }

    /// <summary>
    /// Function to calculate halo width to use when drawing symbol from EvaluationContext
    /// </summary>
    public Func<EvaluationContext, float>? HaloWidth { get; init; }

    /// <summary>
    /// Function to calculate translate of symbols point from EvaluationContext in pixels
    /// </summary>
    public Func<EvaluationContext, Point>? Translate { get; init; }

    /// <summary>
    /// Function to calculate anchor of translate (map or viewport) from EvaluationContext
    /// </summary>
    public Func<EvaluationContext, MapAlignment>? TranslateAnchor { get; init; }

    /// <summary>
    /// Maximum width for the text. If the text exceeds the max width, it will be wrapped.
    /// </summary>
    public Func<EvaluationContext, float, float>? MaxWidth { get; init; }

    public TextPointSymbol Copy()
    {
        var result = new TextPointSymbol(Tile, Id, Point, Text)
        {
            Name = Name,
            StyleName = StyleName,
            SortOrder = SortOrder,
            Class = Class,
            Subclass = Subclass,
            Rank = Rank,
            AllowOthers = AllowOthers,
            Envelope = Envelope?.Copy(),
            ScreenEnvelope = ScreenEnvelope?.Copy(),
            Native = Native,
            Optional = Optional,
            AllowOverlap = AllowOverlap,
            Rotation = Rotation,
            RotationAlignment = RotationAlignment,
            Padding = Padding,
            KeepUpright = KeepUpright,
            Anchor = Anchor,
            Offset = Offset,
            Direction = Direction,
            FontNames = FontNames,
            FontSize = FontSize,
            HaloColor = HaloColor,
            HaloBlur = HaloBlur,
            HaloWidth = HaloWidth,
            Translate = Translate,
            TranslateAnchor = TranslateAnchor,
            MaxWidth = MaxWidth,
        };

        return result;
    }
}