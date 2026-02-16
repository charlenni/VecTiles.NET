using NetTopologySuite.Geometries;

namespace VecTiles.Common.Primitives;

public class PointSymbol : Symbol
{
    public PointSymbol(Tile tile, ulong id, Point point, IconPointSymbol? icon, TextPointSymbol? text) : base(tile, id)
    {
        Point = point;
        IconSymbol = icon;
        TextSymbol = text;

        StyleName = icon is not null ? icon.StyleName : text is not null ? text.StyleName :  string.Empty;

        DrawIcon = (IconSymbol != null);
        DrawText = (TextSymbol != null);
        DrawIconWithoutText = (TextSymbol == null) || TextSymbol.Optional;
        DrawTextWithoutIcon = (IconSymbol == null) || IconSymbol.Optional;
    }

    /// <summary>
    /// Point where symbol is placed in tile coordinates
    /// </summary>
    public Point Point { get; }

    /// <summary>
    /// Icon symbol that belongs to this symbol
    /// </summary>
    public readonly IconPointSymbol? IconSymbol;

    /// <summary>
    /// Text symbol that belongs to this symbol
    /// </summary>
    public readonly TextPointSymbol? TextSymbol;

    /// <summary>
    /// Gets a value indicating whether the icon should be drawn.
    /// </summary>
    public bool DrawIcon { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the text should be drawn.
    /// </summary>
    public bool DrawText { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the icon should be drawn without accompanying text.
    /// </summary>
    public bool DrawIconWithoutText { get; }

    /// <summary>
    /// Gets a value indicating whether the icon should be drawn without accompanying text.
    /// </summary>
    public bool DrawTextWithoutIcon { get; }

    public bool HasIcon => IconSymbol != null;

    public bool HasText => TextSymbol != null;

    public void SetDrawFlags(bool drawIcon, bool drawText)
    {
        DrawIcon = drawIcon;
        DrawText = drawText;
    }
}
