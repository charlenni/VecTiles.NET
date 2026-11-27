using NetTopologySuite.Geometries;
using System.Collections.Generic;
using VecTiles.Common.Interfaces;

namespace VecTiles.Common.Primitives;

/// <summary>
/// Abstract base class for symbols that can be drawn on a tile.
/// </summary>
public abstract class Symbol : ISymbol
{
    /// <summary>
    /// Initializes a new symbol with the specified tile and ID.
    /// </summary>
    /// <param name="tile">The tile this symbol belongs to.</param>
    /// <param name="id">The unique identifier for this symbol.</param>
    public Symbol(Tile tile, ulong id)
    {
        Tile = tile;
        Id = id;
    }

    /// <summary>
    /// Tile to which this symbol belongs
    /// </summary>
    public Tile Tile { get; init; }

    /// <summary>
    /// Id of this symbol
    /// </summary>
    public ulong Id { get; init; }

    /// <summary>
    /// Name of feature this symbol belongs to
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Style name of feature this symbol belongs to
    /// </summary>
    public string StyleName { get; init; } = string.Empty;

    /// <summary>
    /// Sort order to use for this symbol
    /// </summary>
    public double SortOrder { get; init; }

    /// <summary>
    /// Class of this symbol as string
    /// </summary>
    public string Class { get; set; } = string.Empty;

    /// <summary>
    /// Subclass of this symbol as string
    /// </summary>
    public string Subclass { get; set; } = string.Empty;

    /// <summary>
    /// Class attribute of the source vector data, independent of the
    /// MapLibre schema: set from the attribute key the source uses for
    /// its class (e.g. "class" or "klasse"). Lets consumers of the symbol
    /// show a fallback for features without name.
    /// </summary>
    public string SourceClass { get; init; } = string.Empty;

    /// <summary>
    /// Subclass attribute of the source vector data (e.g. "subclass" or
    /// "art"); see <see cref="SourceClass"/>.
    /// </summary>
    public string SourceSubclass { get; init; } = string.Empty;

    /// <summary>
    /// Rank of this symbol as integer
    /// </summary>
    public int Rank { get; set; } = 0;

    /// <summary>
    /// Indicates whether other symbols can occupy the same space as this symbol.
    /// </summary>
    public bool AllowOthers { get; init; }

    /// <summary>
    /// Envelope of symbol
    /// </summary>
    public Envelope? Envelope { get; set; }

    /// <summary>
    /// Envelope of symbol on screen. Only for the active layout process.
    /// </summary>
    public Envelope? ScreenEnvelope { get; set; }

    /// <summary>
    /// Property holding a native object for the renderer
    /// </summary>
    public object? Native { get; set; }
}
