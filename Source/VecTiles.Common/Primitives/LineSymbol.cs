using NetTopologySuite.Geometries;

namespace VecTiles.Common.Primitives;

/// <summary>
/// Combined container for an icon line symbol and/or a text line symbol on the same line geometry,
/// together with the flags steering which parts are drawn.
/// </summary>
public class LineSymbol : Symbol
{
    /// <summary>
    /// Initializes a new line symbol from an optional icon symbol and an optional text symbol.
    /// </summary>
    /// <param name="tile">The tile the symbol belongs to.</param>
    /// <param name="id">The unique identifier of the symbol.</param>
    /// <param name="geometry">The line geometry on which the symbol is placed.</param>
    /// <param name="icon">The optional icon symbol.</param>
    /// <param name="text">The optional text symbol.</param>
    public LineSymbol(Tile tile, ulong id, Geometry geometry, IconLineSymbol? icon, TextLineSymbol? text) : base(tile, id)
    {
        Geometry = geometry;
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

    /// <summary>
    /// Gets a value indicating whether this symbol contains an icon symbol.
    /// </summary>
    public bool HasIcon => IconSymbol != null;

    /// <summary>
    /// Gets a value indicating whether this symbol contains a text symbol.
    /// </summary>
    public bool HasText => TextSymbol != null;

    /// <summary>
    /// Space between two symbols in pixel
    /// </summary>
    public float Spacing { get; init; }

    /// <summary>
    /// Sets the draw flags for the icon and text parts of this symbol, e.g. after collision detection.
    /// </summary>
    /// <param name="drawIcon">Whether the icon part should be drawn.</param>
    /// <param name="drawText">Whether the text part should be drawn.</param>
    public void SetDrawFlags(bool drawIcon, bool drawText)
    {
        DrawIcon = drawIcon;
        DrawText = drawText;
    }
}
