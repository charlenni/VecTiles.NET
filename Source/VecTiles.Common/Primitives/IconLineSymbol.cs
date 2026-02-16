using NetTopologySuite.Geometries;
using VecTiles.Common.Enums;
using VecTiles.Common.Interfaces;

namespace VecTiles.Common.Primitives;

public class IconLineSymbol : Symbol
{
    public IconLineSymbol(Tile tile, ulong id, Geometry geometry, ISprite sprite) : base(tile, id)
    {
        Geometry = geometry;
        Icon = sprite;
    }

    /// <summary>
    /// Point where symbol is placed in world coordinates
    /// </summary>
    public Geometry Geometry { get; }

    /// <summary>
    /// Object to use for this symbol
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
    /// Scale of symbol
    /// </summary>
    public float Scale { get; init; }

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
    public Point Anchor { get; init; } = new(0, 0);

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
            
    public IconLineSymbol Copy()
    {
        var result = new IconLineSymbol(Tile, Id, Geometry, Icon)
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
