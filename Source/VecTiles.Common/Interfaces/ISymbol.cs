using VecTiles.Common.Primitives;

namespace VecTiles.Common.Interfaces;

/// <summary>
/// Represents a symbol that can be drawn on a tile.
/// </summary>
public interface ISymbol
{
    /// <summary>
    /// Tile to which this symbol belongs
    /// </summary>
    Tile Tile { get; }

    /// <summary>
    /// Name of feature this symbol belongs to
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Value for sort order of this symbol
    /// </summary>
    double SortOrder { get; }

    /// <summary>
    /// Rank of this symbol for sorting
    /// </summary>
    int Rank { get; }

    /// <summary>
    /// Indicates whether other symbols can occupy the same space as this symbol.
    /// </summary>
    bool AllowOthers { get; }

    /// <summary>
    /// Determines if this symbol should not be drawn for the given tile range.
    /// </summary>
    /// <param name="key">TilesKey for a tile range</param>
    /// <returns>True if symbol should not be drawn in this tile range; otherwise, false.</returns>
    bool DrawNotForTiles(TilesKey key);

    /// <summary>
    /// Adds a tile key to prevent the symbol from being drawn for that tile range.
    /// </summary>
    /// <param name="key">TilesKey for a tile range</param>
    void Add(TilesKey key);

    /// <summary>
    /// Removes a tile key to allow the symbol to be drawn for that tile range.
    /// </summary>
    /// <param name="key">TilesKey for a tile range</param>
    void Remove(TilesKey key);
}
