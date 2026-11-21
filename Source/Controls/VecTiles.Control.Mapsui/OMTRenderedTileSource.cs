using System.Text.RegularExpressions;
using BruTile;
using BruTile.Cache;
using VecTiles.Common.Interfaces;
using VecTiles.Renderers.Common;
using VecTiles.Renderers.Common.Interfaces;
using VecTiles.Renderers.Skia;
using VecTiles.Renderers.Skia.Primitives;
using VecTiles.Styles.OpenMapTiles;
using MapsuiLogger = Mapsui.Logging.Logger;

namespace VecTiles.Controls.Mapsui;

public class OMTRenderedTileSource : IRenderedTileSource
{
    private readonly Renderer _renderer;
    private readonly MemoryCache<IRenderedTile> _cache;
    private readonly OMTStyleFile _styleFile;

    public OMTRenderedTileSource(string source)
    {
        _styleFile = OMTStyleFileLoader.Load(source).Result;

        var omtStyleFile = _styleFile;
        var minZoom = 0;
        var maxZoom = 24;

        _renderer = new Renderer(omtStyleFile.Sources.Select(s => new KeyValuePair<string, ITileDataSource>(s.Key, (ITileDataSource)s.Value.DataSource)), omtStyleFile.Layers, new RenderFactory(omtStyleFile.Layers, new OMTPaintFactory(omtStyleFile.Sprites), new OMTSymbolFactory(omtStyleFile.Sprites)));
        _cache = new MemoryCache<IRenderedTile>(200, 300);

        ReportMissingFonts(omtStyleFile);

        Schema = new GlobalSphericalMercator512(YAxis.OSM, minZoom, maxZoom);  // Default format in VecTiles.NET
        Name = omtStyleFile.Name;

        foreach (var s in omtStyleFile.Sources)
        {
            if (string.IsNullOrEmpty(s.Value.Attribution))
                continue;
            
            var name = RemoveHtmlTags(s.Value.Attribution);
            var url =  ExtractHtmlUrl(s.Value.Attribution) ?? (s.Value.Url ?? string.Empty);
            var attribution = new Attribution(name, url);
            Attribution = attribution;
            break;
        }
    }

    public ITileSchema Schema { get; private set; }

    public string Name { get; private set; }

    public Attribution Attribution { get; private set; }

    /// <summary>
    /// Styles of the loaded style file
    /// </summary>
    public OMTLayerStyle[] Styles => _styleFile.Layers;

    /// <summary>
    /// Center of the loaded style file as longitude/latitude, empty if not set
    /// </summary>
    public float[] Center => _styleFile.Center;

    /// <summary>
    /// Zoom level of the loaded style file, null if not set
    /// </summary>
    public float? Zoom => _styleFile.Zoom;

    /// <summary>
    /// Clear the cache of rendered tiles, so they are rendered again on next access
    /// </summary>
    public void ClearCache()
    {
        _cache.Clear();
    }

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
    
    private static void ReportMissingFonts(OMTStyleFile styleFile)
    {
        var textFonts = styleFile.Layers
            .Where(l => l.Layout?.TextFont is { Length: > 0 })
            .Select(l => l.Layout.TextFont)
            .ToList();

        var fontNames = textFonts.SelectMany(f => f).ToList();

        // Styles that render text without a text-font use the fallback fonts
        // of OMTTextBuilder, so they must be checked as well
        var textStyleLayers = styleFile.Layers
            .Where(l => l.Layout != null && !l.Layout.TextField.HasOnlyDefault)
            .ToList();

        if (textStyleLayers.Any(l => l.Layout.TextFont is not { Length: > 0 }))
        {
            fontNames.AddRange(OMTTextBuilder.DefaultFontNames);
        }

        if (fontNames.Count == 0)
        {
            return;
        }

        var missing = FontCache.GetMissingFonts(fontNames);

        if (missing.Count == 0)
        {
            return;
        }

        FontCache.ReportMissingFonts(styleFile.Name ?? string.Empty, missing);

        MapsuiLogger.Log(global::Mapsui.Logging.LogLevel.Warning,
            $"Style '{styleFile.Name}': font(s) not found, missing: {string.Join(", ", missing)}. " +
            "Labels are drawn with the system default font instead. " +
            "Install the fonts or register font files via FontCache.AddFontSearchPath/RegisterFontFile/RegisterFontDirectory.");
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
        Match match = Regex.Match(input, @"href=""([^""]*)""");

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
