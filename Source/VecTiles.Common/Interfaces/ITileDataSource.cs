using VecTiles.Common.Enums;
using VecTiles.Common.Primitives;

namespace VecTiles.Common.Interfaces;

/// <summary>
/// ITileDataSource defines the contract for tile data sources. Implementations should 
/// provide information about the tile source, such as its name, supported zoom levels, 
/// and the type of data it supplies (e.g., vector, raster, etc.).
/// </summary>
public interface ITileDataSource
{
    /// <summary>
    /// Name of source.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Minimal zoom level for tile source.
    /// </summary>
    int MinZoom { get; }

    /// <summary>
    /// Maximal zoom level for tile source.
    /// </summary>
    int MaxZoom { get; }

    /// <summary>
    /// Type of this source.
    /// </summary>
    SourceType SourceType { get; }

    /// <summary>
    /// Asynchronously retrieves the tile data for the specified tile.
    /// Returns the tile data as a byte array, or null if the tile is not available.
    /// </summary>
    Task<byte[]?> GetTileAsync(Tile tile);
}
