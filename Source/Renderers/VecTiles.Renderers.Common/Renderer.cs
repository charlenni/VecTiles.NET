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

        // Create rendered tile style after style
        foreach (var style in visibleStyles)
        {
            switch (style.StyleType)
            {
                case LayerStyleType.Background:
                    RenderAsBackground(renderedTile, context, style, _renderFactory);
                    break;
                case LayerStyleType.Raster:
                    if (rawTiles.TryGetValue(style.Source.ToLower(CultureInfo.InvariantCulture), out var rasterData))
                    {
                        if (rasterData is null)
                        {
                            continue;
                        }
                        RenderTileAsRaster(renderedTile, context, (byte[])rasterData, style, _renderFactory);
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
                        RenderTilePartAsVectorFill(renderedTile, context, (VectorTile)fillData, style, _renderFactory);
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
                        RenderTilePartAsVectorLine(renderedTile, context, (VectorTile)lineData, style, _renderFactory);
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
                        RenderTilePartAsVectorCircle(renderedTile, context, (VectorTile)circleData, style, _renderFactory);
                        hasContent = true;
                    }
                    break;
                case LayerStyleType.Symbol:
                case LayerStyleType.FillExtrusion:
                case LayerStyleType.Unknown:
                    break;
                default:
                    throw new NotImplementedException($"Style with type '{style.StyleType}' is unknown");
            }
        }

        // Draw symbols in revers order, because last style layer is the top most layer
        for (var i = visibleStyles.Count - 1; i >= 0; i--)
        {
            var style = visibleStyles[i];
            
            if (style.StyleType != LayerStyleType.Symbol)
            {
                continue;
            }

            if (!IsVisible(tile.Zoom, style))
            {
                continue;
            }

            if (rawTiles.TryGetValue(style.Source, out var symbolData))
            {
                if (symbolData is null)
                {
                    continue;
                }
                RenderTilePartAsSymbol(renderedTile, tile, context, (VectorTile)symbolData, style, _renderFactory);
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
        if (!ExtractFeatures(data, style, context, out var features))
        {
            return;
        }

        renderedTile.RenderedLayers.Add(style.Name, renderFactory.CreateVectorFillLayer(style, context, features!));
    }

    private static void RenderTilePartAsVectorLine(IRenderedTile renderedTile, EvaluationContext context, VectorTile data, ILayerStyle style, IRenderFactory renderFactory)
    {
        if (!ExtractFeatures(data, style, context, out var features))
        {
            return;
        }

        renderedTile.RenderedLayers.Add(style.Name, renderFactory.CreateVectorLineLayer(style, context, features!));
    }

    private static void RenderTilePartAsVectorCircle(IRenderedTile renderedTile, EvaluationContext context, VectorTile data, ILayerStyle style, IRenderFactory renderFactory)
    {
        if (!ExtractFeatures(data, style, context, out var features))
        {
            return;
        }

        renderedTile.RenderedLayers.Add(style.Name, renderFactory.CreateVectorCircleLayer(style, context, features!));
    }

    private static void RenderTilePartAsSymbol(IRenderedTile renderedTile, Tile tile, EvaluationContext context, VectorTile data, ILayerStyle style, IRenderFactory renderFactory)
    {
        var symbols = new List<ISymbol>();

        if (!ExtractFeatures(data, style, context, out var features))
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
