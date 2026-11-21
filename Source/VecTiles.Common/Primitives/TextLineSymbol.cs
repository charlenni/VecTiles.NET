using NetTopologySuite.Geometries;
using VecTiles.Common.Enums;

namespace VecTiles.Common.Primitives;

/// <summary>
/// Text symbol that is placed and aligned along a line geometry.
/// </summary>
public class TextLineSymbol : Symbol
{
    /// <summary>
    /// Initializes a new text line symbol.
    /// </summary>
    /// <param name="tile">The tile the symbol belongs to.</param>
    /// <param name="id">The unique identifier of the symbol.</param>
    /// <param name="geometry">The line geometry along which the text is placed.</param>
    /// <param name="text">The text to draw.</param>
    public TextLineSymbol(Tile tile, ulong id, Geometry geometry, string text) : base(tile, id)
    {
        Geometry = geometry;
        Text = text;
    }

    /// <summary>
    /// Geometry where symbol is placed in world coordinates
    /// </summary>
    public Geometry Geometry { get; }

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
    public bool KeepUpright { get; init; } = true;

    /// <summary>
    /// Maximum angle change between consecutive characters in degrees. Labels on
    /// lines with sharper bends are not placed (like MapLibre's text-max-angle).
    /// </summary>
    public Func<EvaluationContext, float>? MaxAngle { get; init; }

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
    /// Line height as multiple of the font size, used when text is wrapped to multiple lines
    /// </summary>
    public float LineHeight { get; init; } = 1.2f;

    /// <summary>
    /// Additional letter spacing between characters
    /// </summary>
    public float LetterSpacing { get; init; } = 0f;

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

    /// <summary>
    /// Creates a copy of this symbol with all its current property values.
    /// </summary>
    /// <returns>A new <see cref="TextLineSymbol"/> with the same values as this symbol.</returns>
    public TextLineSymbol Copy()
    {
        var result = new TextLineSymbol(Tile, Id, Geometry, Text)
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
            MaxAngle = MaxAngle,
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
            LineHeight = LineHeight,
            LetterSpacing = LetterSpacing,
        };

        return result;
    }
}
