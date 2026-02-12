using VecTiles.Common.Primitives;

namespace VecTiles.Common.Interfaces;

/// <summary>
/// ITileConverter defines a contract for converting binary tile data into a VectorTile instance.
/// </summary>
/// <remarks>
/// The conversion may involve transforming data from a provided tile (possibly at a different 
/// zoom level) to match the requested tile's specification.
/// </remarks>
public interface ITileConverter
{
    /// <summary>
    /// Convert tile to a vector tile
    /// </summary>
    /// <param name="requestedTile">Tile we want to have</param>
    /// <param name="providedTile">Tile to which the data belongs. Could be from a lower zoom level.</param>
    /// <param name="data">Binary data from the provided tile</param>
    /// <returns>Vector tile</returns>
    Task<VectorTile?> Convert(Tile requestedTile, Tile providedTile, byte[] data);
}
