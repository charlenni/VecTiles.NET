using BruTile;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Rendering.Skia;
using Mapsui.Tiling.Extensions;
using System.Linq;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Controls.Mapsui.Extensions;
using VecTiles.Renderers.Common;
using VecTiles.Renderers.Common.Interfaces;

namespace VecTiles.Controls.Mapsui;

/// <summary>
/// A Mapsui layer that renders symbols from a rendered tile source.
/// </summary>
public class RenderedSymbolsLayer : BaseLayer
{
    private const string _customLayerRendererName = "rendered-symbols-layer";

    /// <summary>
    /// An empty dictionary of symbol layers for cases where no symbols are available.
    /// </summary>
    public static Dictionary<string, ISymbolLayer> Empty => new();

    private TilesKey _lastKey;
    private ISymbol[] _lastSymbols = Array.Empty<ISymbol>();
    private readonly MRect? _extent;

    /// <summary>
    /// Initializes a new instance of the <see cref="RenderedSymbolsLayer"/> class.
    /// </summary>
    /// <param name="tileSource">The rendered tile source providing the symbols.</param>
    public RenderedSymbolsLayer(IRenderedTileSource tileSource)
    {
        MapRenderer.RegisterLayerRenderer(_customLayerRendererName, RenderedSymbolsLayerRenderer.Draw);

        TileSource = tileSource;
        CustomLayerRendererName = _customLayerRendererName;

        _extent = TileSource.Schema.Extent.ToMRect();
    }

    /// <summary>
    /// Gets the tile source for this layer.
    /// </summary>
    public IRenderedTileSource TileSource { get; private set; }

    /// <inheritdoc />
    public override IReadOnlyList<double> Resolutions => TileSource.Schema.Resolutions.Select(r => r.Value.UnitsPerPixel).ToList();

    /// <inheritdoc />
    public override MRect? Extent => _extent;

    /// <summary>
    /// Gets or sets a value indicating whether to show valid symbol borders for debugging.
    /// </summary>
    public bool ShowValidBorders { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to show invalid symbol borders for debugging.
    /// </summary>
    public bool ShowInvalidBorders { get; set; }

    /// <summary>
    /// Gets or creates symbols for the specified tile information and zoom level.
    /// Uses caching to avoid recreating symbols for the same tile set and zoom level.
    /// </summary>
    /// <param name="tileInfos">The tile information collection.</param>
    /// <param name="zoomLevel">The zoom level.</param>
    /// <returns>The collection of symbols for the specified tiles and zoom level.</returns>
    public IEnumerable<ISymbol> GetOrCreateSymbols(IEnumerable<TileInfo> tileInfos, int zoomLevel)
    {
        var tileInfoArray = tileInfos as TileInfo[] ?? tileInfos.ToArray();
        var key = new TilesKey(tileInfoArray.ToTile());

        if (_lastKey.Equals(key))
        {
            return _lastSymbols;
        }

        var symbolLayers = GetSymbolLayers(tileInfoArray, zoomLevel);
        var symbols = GetSymbols(symbolLayers.Where(kv => kv.Value.MinZoom <= zoomLevel && kv.Value.MaxZoom >= zoomLevel)).ToArray();

        _lastKey = key;
        _lastSymbols = symbols;

        return symbols;
    }

    /// <inheritdoc />
    public override IEnumerable<IFeature> GetFeatures(MRect box, double resolution)
    {
        return Array.Empty<IFeature>();
    }

    /// <summary>
    /// Retrieves symbol layers from the tile source for the specified tile information and zoom level.
    /// </summary>
    /// <param name="tileInfos">The tile information collection.</param>
    /// <param name="zoomLevel">The zoom level (unused but kept for interface consistency).</param>
    /// <returns>A dictionary mapping layer names to symbol layers.</returns>
    private Dictionary<string, ISymbolLayer> GetSymbolLayers(IEnumerable<TileInfo> tileInfos, int zoomLevel)
    {
        var tileInfoArray = tileInfos as TileInfo[] ?? tileInfos.ToArray();
        var tiles = new List<RenderedTileFeature>(tileInfoArray.Length);

        foreach (var tileInfo in tileInfoArray)
        {
            var tile = TileSource.GetTileAsync(tileInfo).Result;
            if (tile != null)
            {
                tiles.Add((RenderedTileFeature)tile);
            }
        }

        if (tiles.Count == 0)
        {
            // Viewport contains no tiles or there is no information for these tiles
            return Empty;
        }

        // Merge symbols of all tiles and style layers into a single dictionary
        var layers = new Dictionary<string, ISymbolLayer>();

        foreach (var tile in tiles)
        {
            if (tile.RenderedTile?.RenderedSymbols is null)
            {
                continue;
            }

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

    /// <summary>
    /// Retrieves and sorts symbols from the specified symbol layers.
    /// Symbols are ordered by SortOrder first, then by Rank.
    /// LineSymbols of the same feature split across multiple tiles are merged
    /// into one continuous symbol, so they can be treated uniformly.
    /// </summary>
    /// <param name="symbolLayers">The symbol layers to process.</param>
    /// <returns>An enumerable of sorted symbols.</returns>
    private IEnumerable<ISymbol> GetSymbols(IEnumerable<KeyValuePair<string, ISymbolLayer>> symbolLayers)
    {
        var symbols = new List<ISymbol>();

        foreach (var symbolLayer in symbolLayers)
        {
            symbols.AddRange(
                LineSymbolMerger.MergeById(
                    symbolLayer.Value.Symbols.OrderBy(s => s.SortOrder).ThenBy(s => s.Rank)));
        }

        return symbols;
    }
}
