using VecTiles.Common.Primitives;

namespace VecTiles.Common.Interfaces;

/// <summary>
/// Data source that is used as tile source
/// </summary>
public interface IRasterTileDataSource : ITileDataSource
{
    /// <summary>
    /// Asynchronously gets the raster tile data for the specified tile.
    /// </summary>
    /// <param name="tile">The tile to retrieve data for.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the tile data as a byte array, or null if not available.</returns>
    Task<byte[]?> GetTileAsync(Tile tile);
}
