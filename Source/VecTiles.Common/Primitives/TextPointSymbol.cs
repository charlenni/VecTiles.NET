using NetTopologySuite.Geometries;
using VecTiles.Common.Enums;
using VecTiles.Common.Extensions;
// The class has a property named "Anchor", so the Anchor enum needs an alias
using AnchorEnum = VecTiles.Common.Enums.Anchor;

namespace VecTiles.Common.Primitives;

/// <summary>
/// Text symbol that is placed at a point geometry.
/// </summary>
public class TextPointSymbol : Symbol
{
    /// <summary>
    /// Initializes a new text point symbol.
    /// </summary>
    /// <param name="tile">The tile the symbol belongs to.</param>
    /// <param name="id">The unique identifier of the symbol.</param>
    /// <param name="point">The point where the text is placed.</param>
    /// <param name="text">The text to draw.</param>
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
    /// Anchor of symbol given as relative position with [0..1, 0..1].
    /// Mutable: the placement tries the variable anchors (see below) and
    /// stores the anchor of the placement that found free space.
    /// </summary>
    public Point Anchor { get; set; } = new(0, 0);

    /// <summary>
    /// Offset from point in pixels. Mutable: belongs to the anchor chosen
    /// by the placement of a variable anchor text.
    /// </summary>
    public Point Offset { get; set; } = new(0, 0);

    /// <summary>
    /// Candidate anchors (text-variable-anchor of the Mapbox Style Spec) the
    /// placement tries in order; empty for a fixed anchor.
    /// </summary>
    public Anchor[] VariableAnchors { get; init; } = [];

    /// <summary>
    /// The pixel offset belonging to every candidate anchor of
    /// <see cref="VariableAnchors"/> (same order), the distance the text is
    /// pushed away from the symbol point (text-radial-offset/text-offset).
    /// </summary>
    public Point[] VariableOffsets { get; init; } = [];

    /// <summary>
    /// Stores the placement of a chosen variable anchor: the anchor and offset
    /// of the candidate that found free space, and - for text-justify auto -
    /// the alignment towards the anchor position.
    /// </summary>
    /// <param name="anchor">The chosen candidate anchor.</param>
    /// <param name="offset">The pixel offset of the candidate.</param>
    public void UsePlacement(AnchorEnum anchor, Point offset)
    {
        Anchor = anchor.ToPoint();
        Offset = offset;

        if (JustifyAuto)
        {
            Alignment = anchor switch
            {
                AnchorEnum.Left or AnchorEnum.TopLeft or AnchorEnum.BottomLeft => TextJustify.Left,
                AnchorEnum.Right or AnchorEnum.TopRight or AnchorEnum.BottomRight => TextJustify.Right,
                _ => TextJustify.Center,
            };
        }
    }

    /// <summary>
    /// Function to calculate color to use when drawing symbol from EvaluationContext
    /// </summary>
    public Func<EvaluationContext, Color>? Color { get; init; } = (ctx) => Colors.Black;

    /// <summary>
    /// Function to calculate opacity of symbol from EvaluationContext
    /// </summary>
    public Func<EvaluationContext, float>? Opacity { get; init; } = (ctx) => 1f;

    /// <summary>
    /// Text alignment in MaxWidth. Mutable when <see cref="JustifyAuto"/> is
    /// set: the alignment follows the anchor chosen by the placement.
    /// </summary>
    public TextJustify Alignment { get; set; }

    /// <summary>
    /// Whether the style asks for automatic justification (text-justify auto),
    /// which aligns the text towards the anchor position: left for a left
    /// anchor, right for a right anchor, centered otherwise.
    /// </summary>
    public bool JustifyAuto { get; init; }
    
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
    public Func<EvaluationContext, Color>? HaloColor { get; init; } = (ctx) => Colors.Empty;

    /// <summary>
    /// Function to calculate halo blur to use when drawing symbol from EvaluationContext
    /// </summary>
    public Func<EvaluationContext, float>? HaloBlur { get; init; } = (ctx) => 0;

    /// <summary>
    /// Function to calculate halo width to use when drawing symbol from EvaluationContext
    /// </summary>
    public Func<EvaluationContext, float>? HaloWidth { get; init; } = (ctx) => 0;

    /// <summary>
    /// Gets a value indicating whether the halo width is the distance of the halo to the glyph
    /// outline outwards (OpenMapTiles text-halo-width semantics; the renderer doubles it to
    /// compensate the centered glyph stroke), or the stroke width painted over the glyph outline
    /// (Mapsforge caption stroke-width; half of it extends outwards)
    /// </summary>
    public bool HaloIsOutsideDistance { get; init; } = true;

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
    /// <returns>A new <see cref="TextPointSymbol"/> with the same values as this symbol.</returns>
    public TextPointSymbol Copy()
    {
        var result = new TextPointSymbol(Tile, Id, Point, Text)
        {
            Name = Name,
            StyleName = StyleName,
            SortOrder = SortOrder,
            Class = Class,
            Subclass = Subclass,
            SourceClass = SourceClass,
            SourceSubclass = SourceSubclass,
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
            VariableAnchors = VariableAnchors,
            VariableOffsets = VariableOffsets,
            JustifyAuto = JustifyAuto,
            Direction = Direction,
            FontNames = FontNames,
            FontSize = FontSize,
            Color = Color,
            Opacity = Opacity,
            HaloColor = HaloColor,
            HaloBlur = HaloBlur,
            HaloWidth = HaloWidth,
            HaloIsOutsideDistance = HaloIsOutsideDistance,
            Translate = Translate,
            TranslateAnchor = TranslateAnchor,
            MaxWidth = MaxWidth,
            LineHeight = LineHeight,
            LetterSpacing = LetterSpacing,
        };

        return result;
    }
}