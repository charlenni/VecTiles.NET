using System.Text.RegularExpressions;
using BruTile;
using BruTile.Cache;
using Mapsui;
using VecTiles.Common.Interfaces;
using VecTiles.Controls.Mapsui.Extensions;
using VecTiles.Renderers.Common;
using VecTiles.Renderers.Skia;
using VecTiles.Styles.OpenMapTiles;

namespace VecTiles.Controls.Mapsui;

public class OMTRenderedTileSource : IRenderedTileSource
{
    private readonly Renderer _renderer;
    private readonly MemoryCache<RenderedTileFeature> _cache;

    public OMTRenderedTileSource(Stream stream)
    {
        var omtStyleFile = OMTStyleFileLoader.Load(stream).Result;

        var minZoom = 0;
        var maxZoom = 24;

        _renderer = new Renderer(omtStyleFile.Sources.Select(s => new KeyValuePair<string, ITileDataSource>(s.Key, (ITileDataSource)s.Value.DataSource)), omtStyleFile.Layers, new RenderFactory(omtStyleFile.Layers, new OMTPaintFactory(omtStyleFile.Sprites), new OMTSymbolFactory(omtStyleFile.Sprites)));
        _cache = new MemoryCache<RenderedTileFeature>(200, 300);

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

    public async Task<IFeature?> GetTileAsync(TileInfo tileInfo)
    {
        var feature = _cache.Find(tileInfo.Index);

        if (feature != null)
        {
            return (IFeature?)feature;
        }

        var renderedTile = await _renderer.Render(tileInfo.ToTile());
        if (renderedTile == null)
            return null;
        feature = new RenderedTileFeature(renderedTile, tileInfo.Extent.MinX, tileInfo.Extent.MinY, tileInfo.Extent.MaxX, tileInfo.Extent.MaxY);
        _cache.Add(tileInfo.Index, feature);

        return feature;
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
