using System.Text.Json;
using System.Text.Json.Serialization;
using VecTiles.Common.Enums;
using VecTiles.Common.Interfaces;
using VecTiles.DataSources;
using VecTiles.DataSources.MbTiles;
using VecTiles.DataSources.PmTiles;
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
    /// <summary>
    /// Attribution (copyright notice) displayed for the source.
    /// </summary>
    [JsonPropertyName("attribution")]
    public string Attribution { get; set; } = string.Empty;

    /// <summary>
    /// Longitude/latitude bounds limiting which tiles are requested for the source.
    /// </summary>
    [JsonPropertyName("bounds")]
    public float[] Bounds { get; set; } = [-180.0f, -85.051129f, 180.0f, 85.051129f];

    /// <summary>
    /// Size of the tile buffer on each side, in pixels; 0 means no buffer.
    /// </summary>
    [JsonPropertyName("buffer")]
    public int Buffer { get; set; } = 128;

    /// <summary>
    /// Whether point features of a GeoJSON source are clustered.
    /// </summary>
    [JsonPropertyName("cluster")]
    public bool Cluster { get; set; } = false;

    /// <summary>
    /// Zoom level at which point clustering is disabled and points are drawn individually.
    /// </summary>
    [JsonPropertyName("clusterMaxZoom")]
    public float ClusterMaxZoom { get; set; } = 0.0f;

    /// <summary>
    /// Minimum number of points needed to form a cluster.
    /// </summary>
    [JsonPropertyName("clusterMinPoints")]
    public int ClusterMinPoints { get; set; } = 2;

    // clusterProperties

    /// <summary>
    /// Radius of each cluster when clustering point data, in pixels.
    /// </summary>
    [JsonPropertyName("clusterRadius")]
    public int ClusterRadius { get; set; } = 50;

    // data

    /// <summary>
    /// Whether the source data can change dynamically and be reloaded.
    /// </summary>
    [JsonPropertyName("dynamic")]
    public bool Dynamic { get; set; } = false;

    /// <summary>
    /// Vertical encoding of the terrain (raster-dem sources).
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    [JsonPropertyName("encoding")]
    public Encoding Encoding { get; set; } = Encoding.OpenMapTiles;

    /// <summary>
    /// Filter limiting the features of the source that are used.
    /// </summary>
    [JsonConverter(typeof(ExpressionConverter))]
    [JsonPropertyName("filter")]
    public IExpression Filter { get; set; } = Expression.Empty;

    /// <summary>
    /// Whether feature ids are generated for a GeoJSON source that has none.
    /// </summary>
    [JsonPropertyName("generateId")]
    public bool GenerateId { get; set; } = false;

    /// <summary>
    /// Whether per-line metrics (distance, indexes) are computed for a GeoJSON line source.
    /// </summary>
    [JsonPropertyName("lineMetrics")]
    public bool LineMetrics { get; set; } = false;

    /// <summary>
    /// Id of the source, as used by the style layers' "source" property.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Maximum zoom level of the source; tiles above are upscaled from this level.
    /// </summary>
    [JsonPropertyName("maxzoom")]
    public int MaxZoom { get; set; } = 22;

    /// <summary>
    /// Minimum zoom level of the source; tiles below are downscaled from this level.
    /// </summary>
    [JsonPropertyName("minzoom")]
    public int MinZoom { get; set; } = 0;

    // promoteId

    /// <summary>
    /// Names of the raster layers to use from the source.
    /// </summary>
    [JsonPropertyName("rasterLayers")]
    public string RasterLayers { get; set; } = string.Empty;

    /// <summary>
    /// Tile coordinate scheme of the source; xyz or tms.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    [JsonPropertyName("scheme")]
    public Scheme Scheme { get; set; } = Scheme.Xyz;

    /// <summary>
    /// Type of the source, e.g. vector, raster, or geojson.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    [JsonPropertyName("type")]
    public SourceType SourceType { get; set; }

    /// <summary>
    /// Tile URL templates, e.g. "https://example.com/{z}/{x}/{y}.pbf".
    /// </summary>
    [JsonPropertyName("tiles")]
    public IList<string>? Tiles { get; set; }

    /// <summary>
    /// Tile size of the source, in pixels; 512 (MapLibre/OMT) or 256.
    /// </summary>
    [JsonPropertyName("tileSize")]
    public int TileSize { get; set; } = 512;

    /// <summary>
    /// URL pointing to a TileJSON resource describing the source.
    /// </summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    /// <summary>
    /// Alternative tile URL templates used instead of "tiles".
    /// </summary>
    [JsonPropertyName("urls")]
    public string[] Urls { get; set; } = [];

    /// <summary>
    /// Whether the source data is volatile and can change between loads.
    /// </summary>
    [JsonPropertyName("volatile")]
    public bool Volatile { get; set; } = false;

    /// <summary>
    /// Name of the source, as assigned by its key in the style file's "sources" dictionary.
    /// </summary>
    public string Name { get; internal set; } = string.Empty;

    /// <summary>
    /// The tile data source created from this source description, or null until
    /// <see cref="CreateAsync"/> has run successfully.
    /// </summary>
    public ITileDataSource? DataSource { get; internal set; }

    /// <summary>
    /// Sets the tile data source from outside of <see cref="CreateAsync"/> (e.g. a Mapsforge
    /// map file given to the tile source independently of the style file replaces the vector
    /// data source the style declares).
    /// </summary>
    /// <param name="dataSource">The tile data source of this source.</param>
    public void SetDataSource(ITileDataSource? dataSource)
    {
        DataSource = dataSource;
    }

    /// <summary>
    /// Factory for the tile converter that wraps a created vector data source,
    /// because which converter is needed depends on the tile data format (a
    /// mapsforge map file provides raw mapsforge tiles that must be converted
    /// into the OpenMapTiles schema first, ordinary OpenMapTiles/MVT sources are
    /// converted with the identity tags converter). The consumer of the style
    /// file provides the factory, the style layer itself stays independent of
    /// the converters.
    /// Not part of the style source specification.
    /// </summary>
    [JsonIgnore]
    public Func<ITileDataSource, ITileConverter>? VectorTileConverterFactory { get; set; }

    /// <summary>
    /// Center of the underlying tile data source as longitude/latitude, e.g. read
    /// from the metadata of an MBTiles file, empty if the data source provides none.
    /// Not part of the style source specification.
    /// </summary>
    [JsonIgnore]
    public float[] Center { get; internal set; } = [];

    /// <summary>
    /// Zoom level of the underlying tile data source, e.g. read from the metadata
    /// of an MBTiles file, null if the data source provides none.
    /// Not part of the style source specification.
    /// </summary>
    [JsonIgnore]
    public float? Zoom { get; internal set; }

    /// <summary>
    /// Resolves the relative file URLs of the source (mbtiles://, mapsforge://,
    /// file://) against the given base directory, so they can point to files
    /// that live next to the style file.
    /// </summary>
    /// <param name="baseDirectory">Directory the relative URLs are resolved against.</param>
    internal void ResolveRelativeUrls(string baseDirectory)
    {
        Url = ResolveRelativeUrl(Url, baseDirectory);

        if (Tiles != null)
        {
            for (var i = 0; i < Tiles.Count; i++)
                Tiles[i] = ResolveRelativeUrl(Tiles[i], baseDirectory);
        }
    }

    /// <summary>
    /// Resolves a single source URL with a file based scheme against a base
    /// directory. Absolute paths and URLs of other schemes are returned unchanged.
    /// </summary>
    /// <param name="url">The source URL to resolve, may be null.</param>
    /// <param name="baseDirectory">Directory the relative URLs are resolved against.</param>
    /// <returns>The resolved URL, or the unchanged input.</returns>
    private static string? ResolveRelativeUrl(string? url, string baseDirectory)
    {
        if (string.IsNullOrEmpty(url))
            return url;

        var separatorIndex = url.IndexOf("://", StringComparison.Ordinal);
        if (separatorIndex <= 0)
            return url;

        var scheme = url.Substring(0, separatorIndex);
        if (scheme is not (DefaultTileDataSourceFactory.FileScheme or DefaultTileDataSourceFactory.MbTilesScheme or DefaultTileDataSourceFactory.MapsforgeScheme or DefaultTileDataSourceFactory.PmTilesScheme))
            return url;

        var path = url.Substring(separatorIndex + 3);

        if (scheme == DefaultTileDataSourceFactory.PmTilesScheme)
        {
            // A pmtiles source is 'pmtiles://file://path' or 'pmtiles://http(s)://url'; only the
            // file variant is resolved against the base directory. The pmtiles scheme is kept,
            // so the data source factory can route the URL to the PMTiles data source.
            if (!path.StartsWith(DefaultTileDataSourceFactory.FileScheme + "://", StringComparison.Ordinal))
                return url;

            path = path.Substring(DefaultTileDataSourceFactory.FileScheme.Length + 3);

            // Absolute paths and paths relative to the working directory are
            // resolved by the data source itself
            if (Path.IsPathRooted(path) || File.Exists(path))
                return url;

            var resolvedPath = baseDirectory.Length > 0
                ? Path.Combine(baseDirectory, path)
                : path;

            // Only use the resolved path when it exists, otherwise keep the
            // original URL, e.g. when the path is relative to the working
            // directory of an application running from a different directory
            return File.Exists(resolvedPath)
                ? $"{DefaultTileDataSourceFactory.PmTilesScheme}://{resolvedPath}"
                : url;
        }

        if (Path.IsPathRooted(path))
            return url;

        return $"{scheme}://{(baseDirectory.Length > 0 ? baseDirectory.TrimEnd('/') + "/" : string.Empty)}{path}";
    }

    /// <summary>
    /// Creates the actual tile data source from the source description, resolving a
    /// TileJSON reference if necessary and wrapping vector data with the
    /// OpenMapTiles tile converter.
    /// </summary>
    /// <returns>A task that completes when the data source has been created.</returns>
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

        if (dataSource is MbTilesTileDataSource mbTiles)
        {
            Center = mbTiles.Center;
            Zoom ??= mbTiles.CenterZoom;
        }
        else if (dataSource is PmTilesTileDataSource pmTiles)
        {
            Center = pmTiles.Center;
            Zoom ??= pmTiles.CenterZoom;
        }

        if (dataSource.Attribution is not null)
            Attribution = dataSource.Attribution;
        
        if (SourceType == SourceType.Vector)
        {
            // Which converter a vector source needs depends on the tile data format:
            // the tiles of a mapsforge file are raw mapsforge data, their source layers
            // and tags follow the OpenMapTiles schema only after the conversion of the
            // mapsforge tile converter; OpenMapTiles data is already organized in the
            // source layers and tags of the style, so its converter gets the (strict)
            // identity tags converter. The style layer itself stays independent of the
            // converters, the consumer of the style file provides the factory.
            var converter = VectorTileConverterFactory?.Invoke(dataSource)
                ?? throw new InvalidOperationException(
                    $"Vector source '{Name}' is missing a {nameof(VectorTileConverterFactory)}.");
            dataSource = new VectorTileDataSource(dataSource, converter);
        }
        
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
