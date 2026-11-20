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
using VecTiles.Renderers.Skia.Primitives;

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
    /// <param name="fontSourceResolver">
    /// Optional resolver that maps a font name (as in the style file, e.g.
    /// "Noto Sans Bold") to a source in URL notation (file://, https://,
    /// embedded://, ...), from which the font file is loaded via
    /// <see cref="VecTiles.DataSources.ByteDataSource.GetBytesAsync"/> when
    /// the font is neither registered nor installed on the system. Returning
    /// null or an empty string leaves the font to the usual resolution order.
    /// The resolver is held globally by <see cref="FontCache"/>; the resolver
    /// of the last created layer wins.
    /// </param>
    public RenderedSymbolsLayer(IRenderedTileSource tileSource, Func<string, string?>? fontSourceResolver = null)
    {
        MapRenderer.RegisterLayerRenderer(_customLayerRendererName, RenderedSymbolsLayerRenderer.Draw);

        TileSource = tileSource;
        CustomLayerRendererName = _customLayerRendererName;

        FontSourceResolver = fontSourceResolver;
        if (fontSourceResolver != null)
        {
            FontCache.SetFontSourceResolver(fontSourceResolver);
        }

        _extent = TileSource.Schema.Extent.ToMRect();
    }

    /// <summary>
    /// Gets the tile source for this layer.
    /// </summary>
    public IRenderedTileSource TileSource { get; private set; }

    /// <summary>
    /// Gets the optional resolver that maps a font name to a source from
    /// which the font file is loaded when the font is missing. Null if no
    /// resolver was passed to the constructor.
    /// </summary>
    public Func<string, string?>? FontSourceResolver { get; }

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
    /// Clears the cached symbols, so they are recreated on next access.
    /// </summary>
    public void ClearCache()
    {
        _lastKey = default;
        _lastSymbols = Array.Empty<ISymbol>();
    }

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
