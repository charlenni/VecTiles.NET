using BruTile;

namespace VecTiles.Renderers.Common.Interfaces;

/// <summary>
/// Source of rendered tiles: the tiles already carry the drawn layers in
/// style file order (see <see cref="IRenderedTile.Layers"/>). The rendering
/// of the tile data happens lazily and is cached in the implementation.
/// </summary>
public interface IRenderedTileSource : ITileSource
{
    /// <summary>
    /// Center of the loaded style file as longitude/latitude, empty if not set
    /// </summary>
    public float[] Center { get; }

    /// <summary>
    /// Zoom level of the loaded style file, null if not set
    /// </summary>
    public float? Zoom  { get; }

    /// <summary>
    /// Gets the rendered tile for the given tile info, or null when no tile
    /// exists for the index.
    /// </summary>
    Task<IRenderedTile?> GetTileAsync(TileInfo tileInfo);
}
