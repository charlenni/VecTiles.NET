using VecTiles.Common.Primitives;

namespace VecTiles.Common.Interfaces;

/// <summary>
/// Represents a symbol that can be drawn on a tile.
/// </summary>
public interface ISymbol
{
    /// <summary>
    /// Unique id of the feature this symbol belongs to
    /// </summary>
    ulong Id { get; }

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
}
