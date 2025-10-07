using NetTopologySuite.Geometries;
using VecTiles.Common.Primitives;

namespace VecTiles.Styles.Mapbox;

public class PointSymbol : Symbol
{
    bool _drawIconWithoutText;
    bool _drawTextWithoutIcon;
    bool _drawIcon;
    bool _drawText;

    public PointSymbol(Tile tile, Point point, IconPointSymbol? icon, TextPointSymbol? text) : base(tile)
    {
        Point = point;
        IconSymbol = icon;
        TextSymbol = text;

        _drawIcon = (IconSymbol != null);
        _drawText = (TextSymbol != null);
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
    public IconPointSymbol? IconSymbol;

    /// <summary>
    /// Gets a value indicating whether the icon should be drawn.
    /// </summary>
    public bool DrawIcon => _drawIcon;

    /// <summary>
    /// Gets a value indicating whether the text should be drawn.
    /// </summary>
    public bool DrawText => _drawText;

    /// <summary>
    /// Gets a value indicating whether the icon should be drawn without accompanying text.
    /// </summary>
    public bool DrawIconWithoutText => _drawIconWithoutText;

    /// <summary>
    /// Gets a value indicating whether the icon should be drawn without accompanying text.
    /// </summary>
    public bool DrawTextWithoutIcon => _drawTextWithoutIcon;

    /// <summary>
    /// Text symbol that belongs to this symbol
    /// </summary>
    public TextPointSymbol? TextSymbol;

    public bool HasIcon => IconSymbol != null;

    public bool HasText => TextSymbol != null;
}
