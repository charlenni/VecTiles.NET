using BruTile;
using Mapsui;

namespace VecTiles.Controls.Mapsui;

public interface IRenderedTileSource : ITileSource
{
    Task<IFeature?> GetTileAsync(TileInfo tileInfo);
}
