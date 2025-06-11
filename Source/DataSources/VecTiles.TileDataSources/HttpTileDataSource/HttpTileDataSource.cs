using BruTile.Predefined;
using BruTile.Web;
using VecTiles.Common.Enums;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;

namespace VecTiles.DataSources.HttpTileDataSource;

public class HttpTileDataSource : ITileDataSource
{
    HttpTileSource _httpTileSource;
    HttpClientHandler _httpHandler;
    HttpClient _httpClient;

    public HttpTileDataSource(string[] sources, int minZoom, int maxZoom) 
    {
        _httpTileSource = new HttpTileSource(new GlobalSphericalMercator(minZoom, maxZoom), sources[0]);
        _httpHandler = new HttpClientHandler { AllowAutoRedirect = true };
        _httpClient = new HttpClient(_httpHandler);
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", $"user-agent-of-{Path.GetFileNameWithoutExtension(System.AppDomain.CurrentDomain.FriendlyName)}");
    }

    /// <inheritdoc/>
    public string Name => _httpTileSource.Name;

    /// <inheritdoc/>
    public int MinZoom { get; set; }

    /// <inheritdoc/>
    public int MaxZoom { get; set; }

    /// <inheritdoc/>
    public SourceType SourceType { get; set; }

    /// <inheritdoc/>
    public Task<byte[]?> GetBytesAsync(string source)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public Task<byte[]?> GetTileAsync(Tile tile)
    {
        return _httpTileSource.GetTileAsync(_httpClient, new BruTile.TileInfo { Index = new BruTile.TileIndex(tile.X, tile.Y, tile.Zoom) });
    }
}
