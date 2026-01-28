using NetTopologySuite.Geometries;
using VecTiles.Common.Interfaces;

namespace VecTiles.Common.Primitives;

public abstract class Symbol(Tile tile) : ISymbol
{
    /// <summary>
    /// Tile to which this symbol belongs
    /// </summary>
    public Tile Tile { get; } = tile;

    /// <summary>
    /// Name of feature this symbol belongs to
    /// </summary>
    public string Name { get; init; } = string.Empty;

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
    /// Rank of this symbol as integer
    /// </summary>
    public int Rank { get; set; } = 0;

    /// <summary>
    /// Could other symbols occupies the same space 
    /// </summary>
    public bool AllowOthers { get; init; }

    // TODO
    // Remove, for test only
    public Envelope? Envelope { get; set; }

    /// <summary>
    /// Property holding a native object for the renderer
    /// </summary>
    public object Renderer { get; set; }
}
