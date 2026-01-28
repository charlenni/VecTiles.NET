using NetTopologySuite.Features;
using SkiaSharp;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Renderers.Common.Interfaces;
using VecTiles.Renderers.Skia.Extensions;

namespace VecTiles.Renderers.Skia;

public class RenderFactory : IRenderFactory
{
    static readonly SKRect _tileRect = new SKRect(0, 0, 512, 512);

    Dictionary<string, IPaint> _paints;
    ISymbolFactory _symbolFactory;
    private ISymbolRenderer _pointSymbolRenderer = new PointSymbolRenderer();
    private ISymbolRenderer _iconPointSymbolRenderer = new IconPointSymbolRenderer();
    private ISymbolRenderer _iconLineSymbolRenderer = new IconLineSymbolRenderer();
    private ISymbolRenderer _textPointSymbolRenderer = new TextPointSymbolRenderer();

    public RenderFactory(IEnumerable<ILayerStyle> styles, IPaintFactory paintFactory, ISymbolFactory symbolFactory)
    {
        _symbolFactory = symbolFactory;
        _paints = new Dictionary<string, IPaint>(styles.Count());

        // Create for each style a IPaint, which then creates later a SKPaint for a given evaluation context
        foreach (var style in styles)
        {
            var paint = paintFactory.CreatePaint(style);

            if (paint != null)
            {
                _paints.Add(style.Name, paint);
            }
        }
    }

    public ILayerRenderer CreateBackgroundLayer(ILayerStyle style)
    {
        return new BackgroundLayerRenderer(_tileRect, _paints[style.Name]);
    }

    public ILayerRenderer CreateRasterLayer(ILayerStyle style, byte[] data)
    {
        using var bitmap = SKBitmap.Decode(data);

        if (bitmap == null)
        {
            throw new Exception("Not possible to decode image");
        }

        return new RasterLayerRenderer(_tileRect, _paints[style.Name], bitmap);
    }

    public ILayerRenderer CreateVectorFillLayer(ILayerStyle style, IEnumerable<IFeature> features)
    {
        var paths = new List<SKPath>(features!.Count());

        // Draw features that belong to a fill style (draw path by path)
        foreach (var feature in features!)
        {
            var pathList = feature.ToSKPath();

            paths.AddRange(pathList);
        }

        return new VectorLayerRenderer(paths, true, _paints[style.Name]);
    }

    public ILayerRenderer CreateVectorLineLayer(ILayerStyle style, IEnumerable<IFeature> features)
    {
        var paths = new SKPath();

        // Draw features that belong to a line style (add path by path and draw them at the end together)
        foreach (var feature in features!)
        {
            var pathList = feature.ToSKPath();
            
            foreach (var path in pathList)
                paths.AddPath(path);
        }

        return new VectorLayerRenderer([paths], false, _paints[style.Name]);
    }

    public ISymbol? CreateSymbol(Tile tile, ILayerStyle style, EvaluationContext context, IFeature feature)
    {
        return _symbolFactory.CreateSymbol(tile, style, context, feature);
    }
}
