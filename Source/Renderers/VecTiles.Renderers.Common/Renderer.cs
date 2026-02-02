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

    public async Task<IRenderedTile?> Render(Tile tile)
    {
        var rawTiles = new Dictionary<string, object>();

        // Get tiles from all sources
        foreach (var source in _sources)
        {
            //if (source.MinZoom > tile.Zoom || source.MaxZoom < tile.Zoom)
            //{
            //    continue;
            //}

            switch (source.Value.SourceType)
            {
                case SourceType.Raster:
                    var binaryTileData = await source.Value.GetTileAsync(tile);
                    if (binaryTileData != null)
                    {
                        rawTiles.Add(source.Key.ToLower(), binaryTileData);
                    }
                    break;
                case SourceType.Vector:
                    var tileData = await ((IVectorTileDataSource)source.Value).GetVectorTileAsync(tile);
                    if (tileData != null)
                    {
                        rawTiles.Add(source.Key.ToLower(), tileData);
                    }
                    break;
            }
        }

        if (!rawTiles.Any())
        {
            // We have no tile information, so don't render the tile
            return null;
        }

        var renderedTile = new RenderedTile(tile);
        var context = new EvaluationContext(tile.Zoom);
        var hasContent = false;

        // Create rendered tile style after style
        foreach (var style in _styles)
        {
            if (!IsVisible(tile.Zoom, style))
            {
                continue;
            }

            switch (style.StyleType)
            {
                case LayerStyleType.Background:
                    RenderAsBackground(renderedTile, context, style, _renderFactory);
                    break;
                case LayerStyleType.Raster:
                    if (rawTiles.ContainsKey(style.Source) && rawTiles[style.Source] != null)
                    {
                        RenderTileAsRaster(renderedTile, context, (byte[])rawTiles[style.Source], style, _renderFactory);
                        hasContent = true;
                    }
                    break;
                case LayerStyleType.Fill:
                    if (rawTiles.ContainsKey(style.Source) && rawTiles[style.Source] != null)
                    {
                        RenderTilePartAsVectorFill(renderedTile, context, (VectorTile)rawTiles[style.Source], style, _renderFactory);
                        hasContent = true;
                    }
                    break;
                case LayerStyleType.Line:
                    if (rawTiles.ContainsKey(style.Source) && rawTiles[style.Source] != null)
                    {
                        RenderTilePartAsVectorLine(renderedTile, context, (VectorTile)rawTiles[style.Source], style, _renderFactory);
                        hasContent = true;
                    }
                    break;
                case LayerStyleType.Symbol:
                case LayerStyleType.FillExtrusion:
                    break;
                default:
                    throw new NotImplementedException($"Style with type '{style.StyleType}' is unknown");
            }
        }

        // Draw symbols in revers order, because last style layer is the top most layer
        foreach (var style in _styles.Reverse())
        {
            if (style.StyleType != LayerStyleType.Symbol)
            {
                continue;
            }

            if (!IsVisible(tile.Zoom, style))
            {
                continue;
            }

            if (rawTiles.ContainsKey(style.Source) && rawTiles[style.Source] != null)
            {
                RenderTilePartAsSymbol(renderedTile, tile, context, (VectorTile)rawTiles[style.Source], style, _renderFactory);
                hasContent = true;
            }
        }

        // Only return rendered tile when it contains more than background
        return hasContent ? renderedTile : null;
    }

    private static void RenderAsBackground(IRenderedTile renderedTile, EvaluationContext context, ILayerStyle style, IRenderFactory renderFactory)
    {
        renderedTile.RenderedLayers.Add(style.Name, renderFactory.CreateBackgroundLayer(style));
    }

    private static void RenderTileAsRaster(IRenderedTile renderedTile, EvaluationContext context, byte[] data, ILayerStyle style, IRenderFactory renderFactory)
    {
        if (data == null || data.Length == 0)
        {
            throw new ArgumentException("Image data is empty", nameof(data));
        }

        renderedTile.RenderedLayers.Add(style.Name, renderFactory.CreateRasterLayer(style, data));
    }

    private static void RenderTilePartAsVectorFill(IRenderedTile renderedTile, EvaluationContext context, VectorTile data, ILayerStyle style, IRenderFactory renderFactory)
    {
        var layer = data.Layers.Where(l => l.Name == style.SourceLayer)?.FirstOrDefault();

        if (!ExtractFeatures(data, style, out var features))
        {
            return;
        }

        renderedTile.RenderedLayers.Add(style.Name, renderFactory.CreateVectorFillLayer(style, features!));
    }

    private static void RenderTilePartAsVectorLine(IRenderedTile renderedTile, EvaluationContext context, VectorTile data, ILayerStyle style, IRenderFactory renderFactory)
    {
        if (!ExtractFeatures(data, style, out var features))
        {
            return;
        }

        renderedTile.RenderedLayers.Add(style.Name, renderFactory.CreateVectorLineLayer(style, features!));
    }

    private static void RenderTilePartAsSymbol(IRenderedTile renderedTile, Tile tile, EvaluationContext context, VectorTile data, ILayerStyle style, IRenderFactory renderFactory)
    {
        var symbols = new List<ISymbol>();

        if (!ExtractFeatures(data, style, out var features))
        {
            // Add SymbolLayer, even if it contains no symbols
            renderedTile.RenderedSymbols.Add(style.Name, new SymbolLayer(symbols));
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

        renderedTile.RenderedSymbols.Add(style.Name, new SymbolLayer(symbols));
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

    private static bool ExtractFeatures(VectorTile data, ILayerStyle style, out IEnumerable<IFeature>? features)
    {
        var layer = data.Layers.Where(l => l.Name == style.SourceLayer)?.FirstOrDefault();

        if (layer == null)
        {
            features = null;
            return false;
        }

        features = layer.Features.Where((f) => style.Filter.Evaluate(f));

        if (features == null || !features.Any())
        {
            return false;
        }

        return true;
    }
}
