using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using VecTiles.Common.Enums;
using VecTiles.Common.Interfaces;
using VecTiles.Converters.Mapbox;
using VecTiles.DataSources;
using VecTiles.Styles.Mapbox.Enums;
using VecTiles.Styles.Mapbox.Expressions;
using VecTiles.Styles.Mapbox.Json.Converter;
using VecTiles.TileDataSources;

namespace VecTiles.Styles.Mapbox;

/// <summary>
/// Source in Json format
/// See https://docs.mapbox.com/style-spec/reference/sources/
/// </summary>
public class MapboxSource
{
    [JsonProperty("attribution")]
    public string Attribution { get; set; } = string.Empty;

    [JsonProperty("bounds")]
    public float[] Bounds { get; set; } = [-180.0f, -85.051129f, 180.0f, 85.051129f];

    [JsonProperty("buffer")]
    public int Buffer { get; set; } = 128;

    [JsonProperty("cluster")]
    public bool Cluster { get; set; } = false;

    [JsonProperty("clusterMaxZoom")]
    public float ClusterMaxZoom { get; set; } = 0.0f;

    [JsonProperty("clusterMinPoints")]
    public int ClusterMinPoints { get; set; } = 2;

    // clusterProperties

    [JsonProperty("clusterRadius")]
    public int ClusterRadius { get; set; } = 50;

    // data

    [JsonProperty("dynamic")]
    public bool Dynamic { get; set; } = false;

    [JsonConverter(typeof(StringEnumConverter))]
    [JsonProperty("encoding")]
    public Encoding Encoding { get; set; } = Encoding.Mapbox;

    [JsonConverter(typeof(ExpressionConverter))]
    [JsonProperty("filter")]
    public IExpression Filter { get; set; }

    [JsonProperty("generateId")]
    public bool GenerateId { get; set; } = false;

    [JsonProperty("lineMetrics")]
    public bool LineMetrics { get; set; } = false;

    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("maxzoom")]
    public int MaxZoom { get; set; } = 22;

    [JsonProperty("minzoom")]
    public int MinZoom { get; set; } = 0;

    // promoteId

    [JsonProperty("rasterLayers")]
    public string RasterLayers { get; set; } = string.Empty;

    [JsonConverter(typeof(StringEnumConverter))]
    [JsonProperty("scheme")]
    public Scheme Scheme { get; set; } = Scheme.Xyz;

    [JsonConverter(typeof(StringEnumConverter))]
    [JsonProperty("type")]
    public SourceType SourceType { get; set; }

    [JsonProperty("tiles")]
    public IList<string> Tiles { get; set; } = [];

    [JsonProperty("tileSize")]
    public int TileSize { get; set; } = 512;

    [JsonProperty("url")]
    public string Url { get; set; } = string.Empty;

    [JsonProperty("urls")]
    public string[] Urls { get; set; } = [];

    [JsonProperty("volatile")]
    public bool Volatile { get; set; } = false;

    public string Name { get; internal set; }

    public ITileDataSource DataSource { get; internal set; }

    public IVectorTileDataSource VectorDataSource { get; internal set; }

    public void Create()
    {
        string sourceUrl = string.Empty;

        if (!string.IsNullOrEmpty(Url))
            sourceUrl = Url;
        else if (Tiles != null && Tiles.Count > 0)
            sourceUrl = Tiles[0];

        var dataSource = DefaultTileDataSourceFactory.CreateTileDataSource([sourceUrl]);

        if (dataSource == null)
            return;

        DataSource = dataSource;

        if (SourceType == SourceType.Vector)
        {
            VectorDataSource = new VectorTileDataSource(DataSource, new MapboxTileConverter());
        }
    }
}
