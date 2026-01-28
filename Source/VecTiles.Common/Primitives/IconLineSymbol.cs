using NetTopologySuite.Geometries;
using VecTiles.Common.Enums;
using VecTiles.Common.Interfaces;

namespace VecTiles.Common.Primitives;

public class IconLineSymbol : Symbol
{
    public IconLineSymbol(Tile tile, Geometry geometry, ISprite sprite) : base(tile)
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
    /// Anchor of symbol given as relative position with [0..1, 0..1]
    /// </summary>
    public Point Anchor { get; init; } = new(0, 0);

    /// <summary>
    /// Offset from point in pixels
    /// </summary>
    public Point Offset { get; init; } = new(0, 0);

    /// <summary>
    /// Space between two symbols in pixel
    /// </summary>
    public float Spacing { get; init; }

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
}
