using VecTiles.Common.Primitives;

namespace VecTiles.Common.Interfaces;

/// <summary>
/// IVectorTileDataSource extends ITileDataSource to provide asynchronous access to vector tiles for a given Tile instance.
/// </summary>
public interface IVectorTileDataSource : ITileDataSource
{
    /// <summary>
    /// Asynchronously retrieves a vector tile for the specified tile.
    /// </summary>
    /// <param name="tile">The tile for which to retrieve the vector tile.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the vector tile, or null if not found.</returns>
    Task<VectorTile?> GetVectorTileAsync(Tile tile);
}
