using NetTopologySuite.Geometries;

namespace VecTiles.Common.Primitives;

public class LineSymbol : Symbol
{
    public LineSymbol(Tile tile, ulong id, Geometry geometry, IconLineSymbol? icon, TextLineSymbol? text) : base(tile, id)
    {
        Geometry = geometry;
        IconSymbol = icon;
        TextSymbol = text;

        DrawIcon = (IconSymbol != null);
        DrawText = (TextSymbol != null);
        DrawIconWithoutText = (TextSymbol == null) || TextSymbol.Optional;
        DrawTextWithoutIcon = (IconSymbol == null) || IconSymbol.Optional;
    }

    /// <summary>
    /// Point where symbol is placed in tile coordinates
    /// </summary>
    public Geometry Geometry { get; }

    /// <summary>
    /// Icon symbol that belongs to this symbol
    /// </summary>
    public readonly IconLineSymbol? IconSymbol;

    /// <summary>
    /// Text symbol that belongs to this symbol
    /// </summary>
    public readonly TextLineSymbol? TextSymbol;

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

    /// <summary>
    /// Space between two symbols in pixel
    /// </summary>
    public float Spacing { get; init; }

    public void SetDrawFlags(bool drawIcon, bool drawText)
    {
        DrawIcon = drawIcon;
        DrawText = drawText;
    }
}
