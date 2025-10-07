using NetTopologySuite.Geometries;
using VecTiles.Common.Interfaces;

namespace VecTiles.Common.Primitives;

public abstract class Symbol : ISymbol
{
    public Symbol(Tile tile)
    {
        Tile = tile;
    }

    /// <summary>
    /// Tile to which this symbol belongs
    /// </summary>
    public Tile Tile { get; }

    /// <summary>
    /// Name of feature this symbol belongs to
    /// </summary>
    public string Name { get; internal set; } = string.Empty;

    /// <summary>
    /// Sort order to use for this symbol
    /// </summary>
    public double SortOrder { get; internal set; }

    /// <summary>
    /// Class of this symbol as string
    /// </summary>
    public string Class { get; internal set; } = string.Empty;

    /// <summary>
    /// Subclass of this symbol as string
    /// </summary>
    public string Subclass { get; internal set; } = string.Empty;

    /// <summary>
    /// Rank of this symbol as integer
    /// </summary>
    public int Rank { get; internal set; } = 0;

    /// <summary>
    /// Could other symbols occupie the same space 
    /// </summary>
    public bool AllowOthers { get; internal set; }

    // TODO
    // Remove, for test only
    public Envelope? Envelope { get; protected set; }
}
