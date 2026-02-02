using BruTile;
using Mapsui;
using Mapsui.Tiling.Fetcher;
using Mapsui.Tiling.Layers;
using Mapsui.Tiling.Rendering;
using VecTiles.Renderers.Common;

namespace VecTiles.Controls.Mapsui;

/// <summary>
/// Layer, which displays a map consisting of individual tiles
/// </summary>
public class RenderedTileLayer : TileLayer
{
    private readonly IRenderedTileSource _renderedTileSource;
    
    /// <summary>
    /// Create tile layer for given rendered tile source
    /// </summary>
    /// <param name="tileSource">Tile source to use for this layer</param>
    /// <param name="minTiles">Minimum number of tiles to cache</param>
    /// <param name="maxTiles">Maximum number of tiles to cache</param>
    /// <param name="dataFetchStrategy">Strategy to get list of tiles for given extent</param>
    /// <param name="renderFetchStrategy"></param>
    /// <param name="minExtraTiles">Number of minimum extra tiles for memory cache</param>
    /// <param name="maxExtraTiles">Number of maximum extra tiles for memory cache</param>
    /// <param name="fetchTileAsFeature">Fetch tile as feature</param>
    /// <param name="httpClient">HttpClient to use</param>
    public RenderedTileLayer(IRenderedTileSource tileSource, int minTiles = 200, int maxTiles = 300,
        IDataFetchStrategy? dataFetchStrategy = null, IRenderFetchStrategy? renderFetchStrategy = null,
        int minExtraTiles = -1, int maxExtraTiles = -1, Func<TileInfo, Task<IFeature?>>? fetchTileAsFeature = null, HttpClient? httpClient = null)
    : base(tileSource, minTiles, maxTiles, dataFetchStrategy, renderFetchStrategy, minExtraTiles, maxExtraTiles, fetchTileAsFeature ?? CreateFetchDelegate(tileSource), httpClient)
    {
        // Save for later use, so that not always has to be converted
        _renderedTileSource = tileSource;
        // Set style of this layer
        Style = new RenderedTileStyle(new TileInformation {Text = true});
    }
    
    private static Func<TileInfo, Task<IFeature?>> CreateFetchDelegate(
        IRenderedTileSource source)
    {
        return source.GetTileAsync;
    }
}
