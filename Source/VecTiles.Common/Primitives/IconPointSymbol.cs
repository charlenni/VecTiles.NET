using NetTopologySuite.Geometries;
using VecTiles.Common.Enums;
using VecTiles.Common.Interfaces;

namespace VecTiles.Common.Primitives;

/// <summary>
/// Point symbol showing an icon at a point geometry.
/// </summary>
public class IconPointSymbol : Symbol
{
    /// <summary>
    /// Initializes a new icon point symbol.
    /// </summary>
    /// <param name="tile">The tile the symbol belongs to.</param>
    /// <param name="id">The unique identifier of the symbol.</param>
    /// <param name="point">The point where the symbol is placed.</param>
    /// <param name="sprite">The sprite used for the icon.</param>
    public IconPointSymbol(Tile tile, ulong id, Point point, ISprite sprite) : base(tile, id)
    {
        Point = point;
        Icon = sprite;
    }

    /// <summary>
    /// Point where symbol is placed in world coordinates
    /// </summary>
    public Point Point { get; }

    /// <summary>
    /// ISprite to use for this symbol
    /// </summary>
    public ISprite Icon { get; }

    /// <summary>
    /// Is symbol optional?
    /// </summary>
    public bool Optional { get; init; }

    /// <summary>
    /// Allow other symbols to overlap this symbol
    /// </summary>
    public bool AllowOverlap { get; init; }

    /// <summary>
    /// Scale of symbol (multiplier on the sprite display size; defaults to 1,
    /// so a symbol factory that does not set a scale draws the sprite 1:1)
    /// </summary>
    public float Scale { get; init; } = 1f;

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
    /// Keep the icon upright, so that is easier to read
    /// </summary>
    public bool KeepUpright { get; init; } = false;

    /// <summary>
    /// Anchor of symbol given as relative position with [0..1, 0..1]
    /// </summary>
    public Point Anchor { get; init; } = new(0.5, 0.5);

    /// <summary>
    /// Offset from point in pixels
    /// </summary>
    public Point Offset { get; init; } = new(0, 0);

    /// <summary>
    /// Function to calculate color filter to use when drawing symbol from EvaluationContext as SKColorFilter
    /// </summary>
    public Func<EvaluationContext, float[]>? ColorFilter { get; init; }

    /// <summary>
    /// Function to calculate opacity of symbol from EvaluationContext
    /// </summary>
    public Func<EvaluationContext, float>? Opacity { get; init; }

    /// <summary>
    /// Function to calculate translate of symbols point from EvaluationContext in pixels
    /// </summary>
    public Func<EvaluationContext, Point>? Translate { get; init; }

    /// <summary>
    /// Function to calculate anchor of translate (map or viewport) from EvaluationContext
    /// </summary>
    public Func<EvaluationContext, MapAlignment>? TranslateAnchor { get; init; }
    
    /// <summary>
    /// Creates a copy of this symbol with all its current property values.
    /// </summary>
    /// <returns>A new <see cref="IconPointSymbol"/> with the same values as this symbol.</returns>
    public IconPointSymbol Copy()
    {
        var result = new IconPointSymbol(Tile, Id, Point, Icon)
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
            Scale = Scale,
            Rotation = Rotation,
            RotationAlignment = RotationAlignment,
            Padding = Padding,
            KeepUpright = KeepUpright,
            Anchor = Anchor,
            Offset = Offset,
            Translate = Translate,
            ColorFilter = ColorFilter,
            Opacity = Opacity,
            TranslateAnchor = TranslateAnchor
        };

        return result;
    }
}
