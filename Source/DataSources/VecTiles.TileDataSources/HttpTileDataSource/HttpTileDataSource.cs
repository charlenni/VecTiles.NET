using System.Net;
using BruTile.Predefined;
using BruTile.Web;
using VecTiles.Common.Enums;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;

namespace VecTiles.DataSources.HttpTileDataSource;

/// <summary>
/// Tile data source that fetches tiles from an HTTP(S) tile server.
/// </summary>
public class HttpTileDataSource : ITileDataSource
{
    HttpTileSource _httpTileSource;
    HttpClientHandler _httpHandler;
    HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpTileDataSource"/> class for the given tile source URL.
    /// </summary>
    /// <param name="sources">List of possible tile source URLs; the first one is used.</param>
    /// <param name="minZoom">Minimum zoom level supported by the source.</param>
    /// <param name="maxZoom">Maximum zoom level supported by the source.</param>
    public HttpTileDataSource(string[] sources, int minZoom, int maxZoom)
    {
        _httpTileSource = new HttpTileSource(new GlobalSphericalMercator(minZoom, maxZoom), sources[0]);
        _httpHandler = new HttpClientHandler { AllowAutoRedirect = true };
        _httpClient = new HttpClient(_httpHandler);
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", $"user-agent-of-{Path.GetFileNameWithoutExtension(System.AppDomain.CurrentDomain.FriendlyName)}");
        // Expose the zoom range of the source: the overzoom fallback of the
        // VectorTileDataSource relies on it
        MinZoom = minZoom;
        MaxZoom = maxZoom;
        // Expose the tile URL templates: consumers like the tile converter factory derive
        // the tile format (e.g. MLT tiles) from them
        Sources = sources;
    }

    /// <summary>
    /// Gets the tile URL templates of this source.
    /// </summary>
    public IReadOnlyList<string> Sources { get; }

    /// <summary>
    /// Gets or sets the tile encoding declared for this source, e.g. the
    /// 'encoding' entry of the TileJSON ('mlt' for MapLibre tile data).
    /// Consumers like the tile converter factory derive the tile format from
    /// it; HTTP response headers don't carry the tile format (tiles are
    /// usually served as 'application/octet-stream').
    /// </summary>
    public string? TileEncoding { get; set; }

    /// <inheritdoc/>
    public string Name => _httpTileSource.Name;

    /// <summary>
    /// Gets or sets the attribution text of the tile source.
    /// </summary>
    public string Attribution { get; set; }

    /// <inheritdoc/>
    public int MinZoom { get; set; }

    /// <inheritdoc/>
    public int MaxZoom { get; set; }

    /// <inheritdoc/>
    public SourceType SourceType { get; set; }

    /// <inheritdoc/>
    public Scheme Scheme { get; set; }

    /// <inheritdoc/>
    public Task<byte[]?> GetBytesAsync(string source)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public async Task<byte[]?> GetTileAsync(Tile tile)
    {
        try
        {
            return await _httpTileSource.GetTileAsync(_httpClient,
                new BruTile.TileInfo { Index = new BruTile.TileIndex(tile.X, tile.Y, tile.Zoom) }).ConfigureAwait(false);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // Tile doesn't exist on the server, e.g. zoom level above the server's maxzoom
            return null;
        }
    }
}
