using BruTile;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Rendering.Skia;
using Mapsui.Tiling.Extensions;
using System.Linq;
using VecTiles.Common.Interfaces;
using VecTiles.Renderers.Common;
using VecTiles.Renderers.Common.Interfaces;
using VecTiles.Renderers.Skia;

namespace VecTiles.Controls.Mapsui;

public class RenderedSymbolsLayer : BaseLayer
{
    private const string customLayerRendererName = "rendered-symbols-layer";
    
    public static Dictionary<string, ISymbolLayer> Empty => new();

    private IEnumerable<TileInfo> _lastTileInfos;
    private IEnumerable<ISymbol> _lastSymbols;
    private readonly MRect? _extent;

    public RenderedSymbolsLayer(IRenderedTileSource tileSource)
    {
        MapRenderer.RegisterLayerRenderer(customLayerRendererName, RenderedSymbolsLayerRenderer.Draw);

        TileSource = tileSource;
        CustomLayerRendererName = customLayerRendererName;
        
        _extent = TileSource.Schema.Extent.ToMRect();
    }

    public IRenderedTileSource TileSource { get; private set; }

    /// <inheritdoc />
    public override IReadOnlyList<double> Resolutions => TileSource.Schema.Resolutions.Select(r => r.Value.UnitsPerPixel).ToList();

    public override MRect? Extent => _extent;

    public bool ShowValidBorders { get; set; }

    public bool ShowInvalidBorders { get; set; }

    public IEnumerable<ISymbol> GetOrCreateSymbols(IEnumerable<TileInfo> tileInfos, int zoomLevel)
    {
        if (_lastTileInfos != null && tileInfos.First().Index.Equals(_lastTileInfos.First().Index) && tileInfos.Last().Index.Equals(_lastTileInfos.Last().Index))
        {
            return _lastSymbols;
        }

        var symbolLayers = GetSymbolLayers(tileInfos, zoomLevel);
        var symbols = GetSymbols(symbolLayers.Where(kv => kv.Value.MinZoom <= zoomLevel & kv.Value.MaxZoom >= zoomLevel));

        _lastTileInfos = tileInfos;
        _lastSymbols = symbols;

        return symbols;
    }

    private Dictionary<string, ISymbolLayer> GetSymbolLayers(IEnumerable<TileInfo> tileInfos, int zoomLevel)
    { 
        var tiles = new List<RenderedTileFeature>(tileInfos.Count());

        foreach (var tileInfo in tileInfos)
        {
            var tile = TileSource.GetTileAsync(tileInfo).Result;
            if (tile != null)
            {
                tiles.Add((RenderedTileFeature)tile);
            }
        }

        if (tiles.Count == 0)
        {
            // Viewport contains no tiles or there are no information for this tiles
            return Empty; 
        }

        // Now sort symbols of all tiles and style layers into one object
        var layers = new Dictionary<string, ISymbolLayer>(tiles[0].RenderedTile.RenderedSymbols.Count);

        foreach (var tile in tiles)
        {
            foreach (var layer in tile.RenderedTile.RenderedSymbols)
            {
                if (!layers.ContainsKey(layer.Key))
                {
                    layers.Add(layer.Key, new SymbolLayer(layer.Value, layer.Value.MinZoom, layer.Value.MaxZoom));
                }
                else
                {
                    layers[layer.Key].Symbols.AddRange(layer.Value.Symbols);
                }
            }
        }

        return layers;
    }

    private IEnumerable<ISymbol> GetSymbols(IEnumerable<KeyValuePair<string, ISymbolLayer>> symbolLayers)
    {
        var symbols = new List<ISymbol>();

        foreach (var symbolLayer in symbolLayers)
        {
            symbolLayer.Value.Symbols.OrderBy(s => s.SortOrder).ThenBy(s => s.Rank).ToList().ForEach(symbols.Add);
        }

        return symbols;
    }

    public override IEnumerable<IFeature> GetFeatures(MRect box, double resolution)
    {
        return null;
    }
}
