using System.Text.Json;
using System.Text.Json.Serialization;
using VecTiles.Common.Enums;
using VecTiles.Common.Interfaces;
using VecTiles.Converters.OpenMapTiles;
using VecTiles.DataSources;
using VecTiles.Styles.OpenMapTiles.Enums;
using VecTiles.Styles.OpenMapTiles.Expressions;
using VecTiles.Styles.OpenMapTiles.Json;
using VecTiles.Styles.OpenMapTiles.Json.Converter;
using VecTiles.TileDataSources;

namespace VecTiles.Styles.OpenMapTiles;

/// <summary>
/// Source in Json format
/// See https://docs.mapbox.com/style-spec/reference/sources/
/// </summary>
public class OMTSource
{
    [JsonPropertyName("attribution")]
    public string Attribution { get; set; } = string.Empty;

    [JsonPropertyName("bounds")]
    public float[] Bounds { get; set; } = [-180.0f, -85.051129f, 180.0f, 85.051129f];

    [JsonPropertyName("buffer")]
    public int Buffer { get; set; } = 128;

    [JsonPropertyName("cluster")]
    public bool Cluster { get; set; } = false;

    [JsonPropertyName("clusterMaxZoom")]
    public float ClusterMaxZoom { get; set; } = 0.0f;

    [JsonPropertyName("clusterMinPoints")]
    public int ClusterMinPoints { get; set; } = 2;

    // clusterProperties

    [JsonPropertyName("clusterRadius")]
    public int ClusterRadius { get; set; } = 50;

    // data

    [JsonPropertyName("dynamic")]
    public bool Dynamic { get; set; } = false;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    [JsonPropertyName("encoding")]
    public Encoding Encoding { get; set; } = Encoding.OpenMapTiles;

    [JsonConverter(typeof(ExpressionConverter))]
    [JsonPropertyName("filter")]
    public IExpression Filter { get; set; } = Expression.Empty;

    [JsonPropertyName("generateId")]
    public bool GenerateId { get; set; } = false;

    [JsonPropertyName("lineMetrics")]
    public bool LineMetrics { get; set; } = false;

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("maxzoom")]
    public int MaxZoom { get; set; } = 22;

    [JsonPropertyName("minzoom")]
    public int MinZoom { get; set; } = 0;

    // promoteId

    [JsonPropertyName("rasterLayers")]
    public string RasterLayers { get; set; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    [JsonPropertyName("scheme")]
    public Scheme Scheme { get; set; } = Scheme.Xyz;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    [JsonPropertyName("type")]
    public SourceType SourceType { get; set; }

    [JsonPropertyName("tiles")]
    public IList<string>? Tiles { get; set; }

    [JsonPropertyName("tileSize")]
    public int TileSize { get; set; } = 512;

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("urls")]
    public string[] Urls { get; set; } = [];

    [JsonPropertyName("volatile")]
    public bool Volatile { get; set; } = false;

    public string Name { get; internal set; } = string.Empty;

    public ITileDataSource? DataSource { get; internal set; }

    public async Task CreateAsync()
    {
        string sourceUrl = string.Empty;

        if (!string.IsNullOrEmpty(Url))
            sourceUrl = Url;
        else if (Tiles != null && Tiles.Count > 0)
            sourceUrl = Tiles[0];

        // If the source points to a TileJSON instead of a tile template,
        // resolve the actual tile urls, zoom range and attribution from it.
        if (sourceUrl.EndsWith(".json", StringComparison.InvariantCultureIgnoreCase))
        {
            sourceUrl = await ResolveTileJson(sourceUrl).ConfigureAwait(false);
        }

        var dataSource = DefaultTileDataSourceFactory.CreateTileDataSource([sourceUrl], MinZoom, MaxZoom);

        if (dataSource == null)
            return;

        if (dataSource.Attribution is not null)
            Attribution = dataSource.Attribution;
        
        if (SourceType == SourceType.Vector)
            dataSource = new VectorTileDataSource(dataSource, new OMTTileConverter());
        
        DataSource = dataSource;
    }

    /// <summary>
    /// Load a TileJSON and update this source with tile urls, zoom range and attribution.
    /// Returns the first tile url of that TileJSON.
    /// </summary>
    private async Task<string> ResolveTileJson(string tileJsonUrl)
    {
        var bytes = await ByteDataSource.GetBytesAsync(tileJsonUrl).ConfigureAwait(false)
            ?? throw new ArgumentException($"Could not load TileJSON '{tileJsonUrl}'");

        var tileJson = JsonSerializer.Deserialize<TileJson>(System.Text.Encoding.UTF8.GetString(bytes), StyleJson.Options)
            ?? throw new ArgumentException($"TileJSON '{tileJsonUrl}' isn't valid");

        if (tileJson.Tiles is { Count: > 0 })
            Tiles = tileJson.Tiles;

        if (tileJson.Minzoom > 0)
            MinZoom = tileJson.Minzoom;

        if (tileJson.Maxzoom > 0)
            MaxZoom = tileJson.Maxzoom;

        if (!string.IsNullOrEmpty(tileJson.Attribution) && string.IsNullOrEmpty(Attribution))
            Attribution = tileJson.Attribution;

        return Tiles![0];
    }

    private class TileJson
    {
        [JsonPropertyName("attribution")]
        public string Attribution { get; set; } = string.Empty;

        [JsonPropertyName("minzoom")]
        public int Minzoom { get; set; } = 0;

        [JsonPropertyName("maxzoom")]
        public int Maxzoom { get; set; } = 0;

        [JsonPropertyName("tiles")]
        public IList<string>? Tiles { get; set; }
    }
}
