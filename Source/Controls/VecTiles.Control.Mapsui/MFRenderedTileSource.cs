using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using BruTile;
using BruTile.Cache;
using VecTiles.Common.Enums;
using VecTiles.Common.Interfaces;
using VecTiles.Converters.Mapsforge;
using VecTiles.DataSources.Mapsforge;
using VecTiles.Renderers.Common;
using VecTiles.Renderers.Common.Interfaces;
using VecTiles.Renderers.Skia;
using VecTiles.Renderers.Skia.Primitives;
using VecTiles.Styles.Mapsforge;
using VecTiles.Styles.OpenMapTiles;
using VecTiles.TileDataSources;
using MapsuiLogLevel = Mapsui.Logging.LogLevel;
using MapsuiLogger = Mapsui.Logging.Logger;

namespace VecTiles.Controls.Mapsui;

/// <summary>
/// A tile source that renders the map data of a Mapsforge map file (*.map) according to a style
/// file, which may be a Mapsforge render theme (XML, matching the tag conventions of the map
/// file) or an OpenMapTiles style file (JSON, expecting the OpenMapTiles tag schema).
/// </summary>
/// <remarks>
/// The style flavour is chosen by the extension of the style source: ".xml" loads a Mapsforge
/// render theme via <see cref="MFRenderThemeLoader"/>, anything else (typically a .json) loads
/// an OpenMapTiles style via <see cref="OMTStyleFileLoader"/>. In the OpenMapTiles case the
/// style's <c>sources</c> do not provide data of their own: the vector data always comes from
/// the given Mapsforge map file and every vector source of the style is served from it. Non
/// vector sources of the style (e.g. raster overlays) are created from the style file as usual
/// and skipped when they cannot be loaded.
/// </remarks>
public class MFRenderedTileSource : IRenderedTileSource, IPlacementOrderSource, IDisposable
{
    /// <inheritdoc />
    public bool OrdersPlacementByPriority => true;
    private const string TempFolderName = "VecTilesMapsforge";

    private readonly Renderer _renderer;
    private readonly MemoryCache<IRenderedTile> _cache;
    private readonly ILayerStyle[] _styles;
    private readonly string _name;
    private readonly float[] _center = [];
    private readonly float? _zoom;
    private readonly Dictionary<string, Attribution> _attributions = new();
    private readonly MapsforgeTileDataSource? _mapsforgeDataSource;
    private readonly string? _tempFile;

    /// <summary>
    /// Initializes a new instance of the <see cref="MFRenderedTileSource"/> class from a
    /// Mapsforge map file and a style file of either supported flavour.
    /// </summary>
    /// <param name="mapSource">The Mapsforge map data file (.map) in file path, file:// or
    /// http(s):// notation, optionally prefixed with "mapsforge:"/"mapsforge://". An http(s)
    /// source is downloaded once to a temporary file and served from there (it is deleted when
    /// the tile source is disposed).</param>
    /// <param name="tagConverter">Converts the raw tags of the features to the tag and source
    /// layer conventions of the style. Pass <see cref="IdentityTagsConverter"/> with the source
    /// layer fallback of the theme, i.e. <c>new IdentityTagsConverter("mapsforge")</c> (or
    /// <see cref="IdentityTagsConverter.Instance"/> when the theme references empty source
    /// layers), for a Mapsforge render theme (data file and theme share the tag conventions);
    /// leave null for an OpenMapTiles style, which applies the OpenMapTiles schema.</param>
    /// <param name="styleSource">The style file: a Mapsforge render theme (.xml) or an
    /// OpenMapTiles style file (.json), as path or URL.</param>
    public MFRenderedTileSource(string mapSource, ITagsConverter? tagConverter, string styleSource)
    {
        // Style loaders expect URL notation; a plain file path is normalized to file://
        if (styleSource.IndexOf(':') < 0)
            styleSource = ToFileSource(styleSource);
        var mapPath = LoadMapFile(mapSource);

        _mapsforgeDataSource = new MapsforgeTileDataSource([mapPath], 0, 24);
        _tempFile = mapPath != mapSource ? mapPath : null;

        var vectorDataSource = new VectorTileDataSource(
            _mapsforgeDataSource, new MapsforgeTileConverter(tagConverter));

        if (IsMapsforgeStyle(styleSource))
        {
            var theme = MFRenderThemeLoader.Load(styleSource).Result;

            // Icon and pattern sources of the theme are resolved against the folder of the
            // theme file; the sprite factory of the rendering must use the same location the
            // loader used, so SVG and raster references resolve identically at render time
            var spriteFactory = new MFSpriteFactory(MFRenderThemeLoader.GetThemeBaseDirectory(styleSource));
            _styles = theme.Layers.Cast<ILayerStyle>().ToArray();
            _name = Path.GetFileNameWithoutExtension(styleSource);

            // Mapsforge theme layers render the features of the map file directly; all of them
            // reference the single source layer "mapsforge" of the identity-converted data
            _renderer = new Renderer(
                [new KeyValuePair<string, ITileDataSource>(theme.Source, vectorDataSource)],
                _styles,
                new RenderFactory(_styles, new MFPaintFactory(spriteFactory), new MFSymbolFactory(spriteFactory)));
        }
        else
        {
            // Sources are created manually below, because vector data comes from the map file
            var styleFile = OMTStyleFileLoader.Load(styleSource, createSources: false,
                vectorTileConnectionFactory: DefaultVectorTileConverterFactory.Factory).Result;

            foreach (var omtSource in styleFile.Sources.Values)
            {
                if (omtSource.SourceType == SourceType.Vector)
                {
                    omtSource.SetDataSource(vectorDataSource);
                }
                else
                {
                    // Raster overlays etc. load from their own declaration in the style file;
                    // a source that cannot be created is skipped, its layers render nothing
                    try
                    {
                        omtSource.CreateAsync().Wait();
                    }
                    catch (Exception exception)
                    {
                        MapsuiLogger.Log(MapsuiLogLevel.Warning,
                            $"MFRenderedTileSource '{styleSource}': source '{omtSource.Name}' could not be created: {exception.Message}");
                    }
                }
            }

            _styles = styleFile.Layers;
            _name = styleFile.Name;
            _center = styleFile.Center;
            _zoom = styleFile.Zoom;

            _renderer = new Renderer(
                styleFile.Sources
                    .Where(s => s.Value.DataSource != null)
                    .Select(s => new KeyValuePair<string, ITileDataSource>(s.Key, s.Value.DataSource!)),
                _styles,
                new RenderFactory(_styles, new OMTPaintFactory(styleFile.Sprites), new OMTSymbolFactory(styleFile.Sprites)));

            foreach (var s in styleFile.Sources.Values.Where(s => _styles.Any(l => l.Source == s.Name)))
            {
                if (string.IsNullOrEmpty(s.Attribution))
                    continue;

                var attribution = new Attribution(RemoveHtmlTags(s.Attribution), ExtractHtmlUrl(s.Attribution) ?? (s.Url ?? string.Empty));
                _attributions.Add(s.Name, attribution);
            }
        }

        _cache = new MemoryCache<IRenderedTile>(200, 300);

        Schema = new GlobalSphericalMercator512(YAxis.OSM, 0, 24);  // Default format in VecTiles.NET
        Name = _name;

        Attribution = _attributions.Count > 0 ? _attributions.Values.First() : new Attribution(string.Empty, string.Empty);
    }

    /// <summary>
    /// Tile schema of the rendered tiles, GlobalSphericalMercator with 512x512 tiles.
    /// </summary>
    public ITileSchema Schema { get; private set; }

    /// <summary>
    /// Name of the loaded style file.
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// Attribution of the data source of the style file.
    /// </summary>
    public Attribution Attribution { get; private set; }

    /// <summary>
    /// Attributions of the style file.
    /// </summary>
    public IDictionary<string, Attribution> Attributions => _attributions;

    /// <summary>
    /// Styles of the loaded style file.
    /// </summary>
    public ILayerStyle[] Styles => _styles;

    /// <summary>
    /// Center of the loaded style file as longitude/latitude, empty if not set.
    /// </summary>
    public float[] Center => _center;

    /// <summary>
    /// Zoom level of the loaded style file, null if not set.
    /// </summary>
    public float? Zoom => _zoom;

    /// <summary>
    /// Clear the cache of rendered tiles, so they are rendered again on next access.
    /// </summary>
    public void ClearCache()
    {
        _cache.Clear();
    }

    /// <summary>
    /// Renders the tile for the given tile info, returning a cached tile when available.
    /// </summary>
    /// <param name="tileInfo">Tile info of the tile to render.</param>
    /// <returns>The rendered tile, or null when it could not be rendered.</returns>
    public async Task<IRenderedTile?> GetTileAsync(TileInfo tileInfo)
    {
        var tile = _cache.Find(tileInfo.Index);

        if (tile != null)
        {
            return tile;
        }

        tile = await _renderer.Render(tileInfo.ToTile());
        if (tile == null)
            return null;
        _cache.Add(tileInfo.Index, tile);

        return tile;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _mapsforgeDataSource?.Dispose();

        if (_tempFile != null)
        {
            try
            {
                File.Delete(_tempFile);
            }
            catch (IOException)
            {
                // The download cache file is best effort; a locked file remains and is
                // overwritten by the next use of the same map source
            }
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Determines whether the style source is a Mapsforge render theme (XML file) as opposed to
    /// an OpenMapTiles style file (JSON).
    /// </summary>
    private static bool IsMapsforgeStyle(string styleSource)
    {
        return string.Equals(Path.GetExtension(styleSource), ".xml", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Converts a plain file system path to the file URL notation of the
    /// ByteDataSource ("file://" on Unix-like systems, "file:///" on Windows), as the style
    /// loaders do for their sources too.
    /// </summary>
    private static string ToFileSource(string source)
    {
        var fullPath = Path.GetFullPath(source);

        return Path.DirectorySeparatorChar == '/'
            ? $"file://{fullPath}"
            : $"file:///{fullPath}";
    }

    /// <summary>
    /// Resolves the map source to a local file path, downloading http(s) sources to a file in
    /// the temporary folder (reused when a previous download of the same source is still
    /// present, e.g. of a not disposed tile source in an earlier run).
    /// </summary>
    /// <param name="mapSource">Map source in file path, file://, http(s):// or mapsforge://(.)
    /// notation.</param>
    /// <returns>The local path of the Mapsforge map file.</returns>
    private static string LoadMapFile(string mapSource)
    {
        var source = mapSource.Trim();

        // Mapsforge file sources carry the mapsforge scheme like tile sources in style files
        if (source.StartsWith("mapsforge:", StringComparison.OrdinalIgnoreCase))
            source = source["mapsforge:".Length..];

        if (source.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            source = source.Substring(7);
        }

        if (source.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            source.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return DownloadMapFileAsync(source).GetAwaiter().GetResult();
        }

        return source;
    }

    /// <summary>
    /// Downloads a Mapsforge map file from an http(s) URL to the temporary folder.
    /// </summary>
    /// <param name="url">URL of the Mapsforge map file.</param>
    /// <returns>The local path of the downloaded map file.</returns>
    private static async Task<string> DownloadMapFileAsync(string url)
    {
        var folder = Path.Combine(Path.GetTempPath(), TempFolderName);
        Directory.CreateDirectory(folder);

        // A stable, unique local name per URL, so concurrent tile sources share the download
        var fileName = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url))) + ".map";
        var filePath = Path.Combine(folder, fileName);

        if (File.Exists(filePath) && new FileInfo(filePath).Length > 0)
            return filePath;

        var tempPath = filePath + ".download";

        // The client of the ByteDataSource has a request timeout meant for style resources
        // (styles, sprites, fonts), not for map files, which can be large; a client without a
        // timeout streams the file instead (the server connection aborts on its own terms)
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true })
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };

        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        await using var networkStream = await response.Content.ReadAsStreamAsync();
        await using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await networkStream.CopyToAsync(fileStream);
        await fileStream.FlushAsync();

        File.Move(tempPath, filePath, overwrite: true);

        return filePath;
    }

    private static string RemoveHtmlTags(string input)
    {
        // Entferne alle HTML-Tags mit einem regulären Ausdruck
        var result = Regex.Replace(input, "<.*?>", string.Empty);

        result = Regex.Replace(result, "&copy;", "©");

        return result;
    }

    private static string? ExtractHtmlUrl(string input)
    {
        // Regex, um die erste URL im href-Attribut zu finden
        var match = Regex.Match(input, @"href=""([^""]*)""");

        if (match.Success)
        {
            return match.Groups[1].Value;
        }
        else
        {
            return null;
        }
    }
}
