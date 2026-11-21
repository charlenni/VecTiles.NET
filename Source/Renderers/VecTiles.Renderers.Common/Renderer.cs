using System.Diagnostics;
using System.Globalization;
using NetTopologySuite.Features;
using VecTiles.Common.Enums;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Renderers.Common.Interfaces;

namespace VecTiles.Renderers.Common;

/// <summary>
/// A renderer, that converts data from different tile sources with the given styles
/// to a ReneredTile
/// </summary>
public class Renderer
{
    IEnumerable<KeyValuePair<string, ITileDataSource>> _sources;
    IEnumerable<ILayerStyle> _styles;
    IRenderFactory _renderFactory;

    /// <summary>
    /// Create renderer
    /// </summary>
    /// <param name="sources">Tile sources to use for data</param>
    /// <param name="styles">Tile styles to use to render </param>
    /// <param name="renderFactory">Factory to render tile styles</param>
    public Renderer(IEnumerable<KeyValuePair<string, ITileDataSource>> sources, IEnumerable<ILayerStyle> styles, IRenderFactory renderFactory)
    {
        _sources = sources;
        _styles = styles;
        _renderFactory = renderFactory;
    }

    /// <summary>
    /// Renders the given tile with all visible styles and their sources and
    /// returns the resulting rendered tile. Returns null when no source
    /// provides data for the tile or no style produces content.
    /// </summary>
    /// <param name="tile">Tile to render.</param>
    /// <returns>The rendered tile, or null when there is nothing to render.</returns>
    public async Task<IRenderedTile?> Render(Tile tile)
    {
        var rawTiles = new Dictionary<string, object?>();
        var visibleStyles = _styles.Where(s => IsVisible(tile.Zoom, s)).ToList();
        var visibleSources = visibleStyles.Select(s => s.Source)
            .Where(s => s != "")
            .Distinct()
            .ToList();

        // Get tiles from all sources
        foreach (var source in _sources)
        {
            if (!visibleSources.Contains(source.Key))
            {
                continue;
            }

            switch (source.Value.SourceType)
            {
                case SourceType.Raster:
                    var binaryTileData = await source.Value.GetTileAsync(tile);
                    if (binaryTileData != null)
                    {
                        rawTiles.Add(source.Key.ToLower(CultureInfo.InvariantCulture), binaryTileData);
                    }
                    break;
                case SourceType.Vector:
                    var tileData = await ((IVectorTileDataSource)source.Value).GetVectorTileAsync(tile);
                    if (tileData != null)
                    {
                        rawTiles.Add(source.Key.ToLower(CultureInfo.InvariantCulture), tileData);
                    }
                    break;
            }
        }

        if (rawTiles.Count == 0)
        {
            // We have no tile information, so don't render the tile
            return null;
        }

        var renderedTile = new RenderedTile(tile);
        var context = new EvaluationContext(tile.Zoom);
        var hasContent = false;

        // Create rendered tile style after style. The index into the full
        // (unfiltered) style list travels with every entry, so the drawing
        // can restore the style file order across tiles that miss entries
        // (styles without data in the tile produce no entry)
        var orderedStyles = _styles.ToList();

        foreach (var styleIndexPair in orderedStyles
            .Select((style, index) => (style, index))
            .Where(pair => IsVisible(tile.Zoom, pair.style)))
        {
            var style = styleIndexPair.style;
            var order = styleIndexPair.index;

            switch (style.StyleType)
            {
                case LayerStyleType.Background:
                    RenderAsBackground(renderedTile, context, style, _renderFactory, order);
                    break;
                case LayerStyleType.Raster:
                    if (rawTiles.TryGetValue(style.Source.ToLower(CultureInfo.InvariantCulture), out var rasterData))
                    {
                        if (rasterData is null)
                        {
                            continue;
                        }
                        RenderTileAsRaster(renderedTile, context, (byte[])rasterData, style, _renderFactory, order);
                        hasContent = true;
                    }
                    break;
                case LayerStyleType.Fill:
                    if (rawTiles.TryGetValue(style.Source.ToLower(CultureInfo.InvariantCulture), out var fillData))
                    {
                        if (fillData is null)
                        {
                            continue;
                        }
                        RenderTilePartAsVectorFill(renderedTile, context, (VectorTile)fillData, style, _renderFactory, order);
                        hasContent = true;
                    }
                    break;
                case LayerStyleType.Line:
                    if (rawTiles.TryGetValue(style.Source.ToLower(CultureInfo.InvariantCulture), out var lineData))
                    {
                        if (lineData is null)
                        {
                            continue;
                        }
                        RenderTilePartAsVectorLine(renderedTile, context, (VectorTile)lineData, style, _renderFactory, order);
                        hasContent = true;
                    }
                    break;
                case LayerStyleType.Circle:
                    if (rawTiles.TryGetValue(style.Source.ToLower(CultureInfo.InvariantCulture), out var circleData))
                    {
                        if (circleData is null)
                        {
                            continue;
                        }
                        RenderTilePartAsVectorCircle(renderedTile, context, (VectorTile)circleData, style, _renderFactory, order);
                        hasContent = true;
                    }
                    break;
                case LayerStyleType.Symbol:
                    if (rawTiles.TryGetValue(style.Source.ToLower(CultureInfo.InvariantCulture), out var symbolData))
                    {
                        if (symbolData is null)
                        {
                            continue;
                        }
                        RenderTilePartAsSymbol(renderedTile, tile, context, (VectorTile)symbolData, style, _renderFactory, order);
                        hasContent = true;
                    }
                    break;
                case LayerStyleType.FillExtrusion:
                case LayerStyleType.Unknown:
                    break;
                default:
                    throw new NotImplementedException($"Style with type '{style.StyleType}' is unknown");
            }
        }

        // Only return rendered tile when it contains more than background
        return hasContent ? renderedTile : null;
    }

    private static void RenderAsBackground(IRenderedTile renderedTile, EvaluationContext context, ILayerStyle style, IRenderFactory renderFactory, int order)
    {
        renderedTile.Layers.Add(new RenderedGeometryLayer(style.Name,
            renderFactory.CreateBackgroundLayer(style), style.MinZoom, style.MaxZoom, order));
    }

    private static void RenderTileAsRaster(IRenderedTile renderedTile, EvaluationContext context, byte[] data, ILayerStyle style, IRenderFactory renderFactory, int order)
    {
        if (data == null || data.Length == 0)
        {
            throw new ArgumentException("Image data is empty", nameof(data));
        }

        renderedTile.Layers.Add(new RenderedGeometryLayer(style.Name, renderFactory.CreateRasterLayer(style, data),
            style.MinZoom, style.MaxZoom, order));
    }

    private static void RenderTilePartAsVectorFill(IRenderedTile renderedTile, EvaluationContext context, VectorTile data, ILayerStyle style, IRenderFactory renderFactory, int order)
    {
        if (!ExtractFeatures(data, style, context, out var features))
        {
            return;
        }

        renderedTile.Layers.Add(new RenderedGeometryLayer(style.Name,
            renderFactory.CreateVectorFillLayer(style, context, features!), style.MinZoom, style.MaxZoom, order));
    }

    private static void RenderTilePartAsVectorLine(IRenderedTile renderedTile, EvaluationContext context, VectorTile data, ILayerStyle style, IRenderFactory renderFactory, int order)
    {
        if (!ExtractFeatures(data, style, context, out var features))
        {
            return;
        }

        renderedTile.Layers.Add(new RenderedGeometryLayer(style.Name,
            renderFactory.CreateVectorLineLayer(style, context, features!), style.MinZoom, style.MaxZoom, order));
    }

    private static void RenderTilePartAsVectorCircle(IRenderedTile renderedTile, EvaluationContext context, VectorTile data, ILayerStyle style, IRenderFactory renderFactory, int order)
    {
        if (!ExtractFeatures(data, style, context, out var features))
        {
            return;
        }

        renderedTile.Layers.Add(new RenderedGeometryLayer(style.Name,
            renderFactory.CreateVectorCircleLayer(style, context, features!), style.MinZoom, style.MaxZoom, order));
    }

    private static void RenderTilePartAsSymbol(IRenderedTile renderedTile, Tile tile, EvaluationContext context, VectorTile data, ILayerStyle style, IRenderFactory renderFactory, int order)
    {
        var symbols = new List<ISymbol>();

        if (!ExtractFeatures(data, style, context, out var features))
        {
            // Add SymbolLayer, even if it contains no symbols
            renderedTile.Layers.Add(new RenderedSymbolLayer(style.Name, new SymbolLayer(symbols), style.MinZoom, style.MaxZoom, order));
            return;
        }

        foreach (var feature in features!)
        {
            // TODO
            // Remove. Only for testing
            //if (feature.Attributes.Exists("name") && feature.Attributes.GetOptionalValue("name").ToString() == "Bürkliplatz" && tile.Y == 5738)
            //    System.Diagnostics.Debug.WriteLine($"Name: {feature.Attributes.GetOptionalValue("name")} at {tile}");

            var symbol = renderFactory.CreateSymbol(tile, style, context, feature);

            if (symbol != null)
            {
                symbols.Add(symbol);
            }
        }

        renderedTile.Layers.Add(new RenderedSymbolLayer(style.Name, new SymbolLayer(symbols), style.MinZoom, style.MaxZoom, order));
    }

    private static bool IsVisible(int zoom, ILayerStyle style)
    {
        if (!style.Visible)
        {
            return false;
        }
        // Unset zoom values are per default -1. So, if zoom value is bigger than -1 it is set by style file.
        if (style.MinZoom > -1 && style.MinZoom > zoom)
        {
            return false;
        }
        if (style.MaxZoom > -1 && style.MaxZoom <= zoom)
        {
            return false;
        }

        return true;
    }

    private static bool ExtractFeatures(VectorTile data, ILayerStyle style, EvaluationContext context, out IReadOnlyList<IFeature> features)
    {
        var layer = data.Layers.FirstOrDefault(l => string.Equals(l.Name, style.SourceLayer, StringComparison.Ordinal));

        if (layer == null)
        {
            features = Array.Empty<IFeature>();
            return false;
        }

        var filter = style.Filter;
        var source = layer.Features;
        List<IFeature>? result = null;

        for (var i = 0; i < source.Count; i++)
        {
            var f = source[i];
            if (filter.Evaluate(context.WithFeature(f)) is true)
            {
                (result ??= new List<IFeature>()).Add(f);
            }
        }

        if (result == null)
        {
            features = Array.Empty<IFeature>();
            return false;
        }

        features = result;
        
        return true;
    }}
