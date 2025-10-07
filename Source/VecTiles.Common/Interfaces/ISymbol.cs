using VecTiles.Common.Primitives;

namespace VecTiles.Common.Interfaces;

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

    bool AllowOthers { get; }
}
