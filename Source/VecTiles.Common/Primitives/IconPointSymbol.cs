using NetTopologySuite.Geometries;
using VecTiles.Common.Enums;
using VecTiles.Common.Interfaces;

namespace VecTiles.Common.Primitives;

public class IconPointSymbol : Symbol
{
    public IconPointSymbol(Tile tile, Point point, ISprite sprite) : base(tile)
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
    public bool Optional { get; internal set; }

    /// <summary>
    /// Allow other symbols to overlap this symbol
    /// </summary>
    public bool AllowOverlap { get; internal set; }

    /// <summary>
    /// Scale of symbol
    /// </summary>
    public float Scale { get; internal set; }

    /// <summary>
    /// Rotation of symbol in degrees
    /// </summary>
    public float Rotation { get; internal set; }

    /// <summary>
    /// Padding around symbol in pixel
    /// </summary>
    public int Padding { get; internal set; }

    /// <summary>
    /// Anchor of symbol given as relative position with [0..1, 0..1]
    /// </summary>
    public Point Anchor { get; internal set; } = new(0, 0);

    /// <summary>
    /// Offset from point in pixels
    /// </summary>
    public Point Offset { get; internal set; } = new(0, 0);

    /// <summary>
    /// Function to calculate color filter to use when drawing symbol from EvaluationContext as SKColorFilter
    /// </summary>
    public Func<EvaluationContext, ColorFilter>? ColorFilter { get; internal set; }

    /// <summary>
    /// Function to calculate opacity of symbol from EvaluationContext
    /// </summary>
    public Func<EvaluationContext, float>? Opacity { get; internal set; }

    /// <summary>
    /// Function to calculate translate of symbols point from EvaluationContext in pixels
    /// </summary>
    public Func<EvaluationContext, Point>? Translate { get; internal set; }

    /// <summary>
    /// Function to calculate anchor of translate (map or viewport) from EvaluationContext
    /// </summary>
    public Func<EvaluationContext, MapAlignment>? TranslateAnchor { get; internal set; }
}
