using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Quadtree;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;

namespace VecTiles.Styles.Mapbox;

public class MapboxPointSymbol : MapboxSymbol
{
    bool _drawIconWithoutText;
    bool _drawTextWithoutIcon;
    bool _drawIcon;
    bool _drawText;

    public MapboxPointSymbol(Tile tile, Point point, MapboxIconPointSymbol? icon, MapboxTextPointSymbol? text) : base(tile)
    {
        Point = point;
        IconSymbol = icon;
        TextSymbol = text;

        _drawIconWithoutText = (TextSymbol == null) || TextSymbol.Optional;
        _drawTextWithoutIcon = (IconSymbol == null) || IconSymbol.Optional;
    }

    /// <summary>
    /// Point where symbol is placed in tile coordinates
    /// </summary>
    public Point Point { get; }

    /// <summary>
    /// Icon symbol that belongs to this symbol
    /// </summary>
    public MapboxIconPointSymbol? IconSymbol;

    /// <summary>
    /// Text symbol that belongs to this symbol
    /// </summary>
    public MapboxTextPointSymbol? TextSymbol;

    public bool HasIcon => IconSymbol != null;

    public bool HasText => TextSymbol != null;

    public override bool CheckForSpace(SKCanvas canvas, EvaluationContext context, Quadtree<ISymbol> tree, Func<double, double, (double, double)> worldToScreenConverter, bool showValidBorders = false, bool showUnvalidBorders = false)
    {
        bool spaceForIconAvailable = IconSymbol?.CheckForSpace(canvas, context, tree, worldToScreenConverter, showValidBorders, showUnvalidBorders) ?? false;
        bool spaceForTextAvailable = TextSymbol?.CheckForSpace(canvas, context, tree, worldToScreenConverter, showValidBorders, showUnvalidBorders) ?? false;
        _drawIcon = HasIcon && spaceForIconAvailable && (spaceForTextAvailable || _drawIconWithoutText);
        _drawText = HasText && spaceForTextAvailable && (spaceForIconAvailable || _drawTextWithoutIcon);

        return _drawIcon | _drawText;
    }

    public override void Draw(SKCanvas canvas, EvaluationContext context, ref Quadtree<ISymbol> tree, Func<double, double, (double, double)> worldToScreenConverter)
    {
        if (_drawIcon)
        {
            IconSymbol?.Draw(canvas, context, ref tree, worldToScreenConverter);
        }

        if (_drawText)
        {
            TextSymbol?.Draw(canvas, context, ref tree, worldToScreenConverter);
        }
    }
}
