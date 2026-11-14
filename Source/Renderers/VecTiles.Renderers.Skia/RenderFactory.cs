using System.Diagnostics;
using NetTopologySuite.Features;
using SkiaSharp;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Renderers.Common.Interfaces;
using VecTiles.Renderers.Skia.Extensions;
using VecTiles.Renderers.Skia.Primitives.Vertices;

namespace VecTiles.Renderers.Skia;

public class RenderFactory : IRenderFactory
{
    static readonly SKRect _tileRect = new SKRect(0, 0, 512, 512);

    Dictionary<string, IPaint> _paints;
    ISymbolFactory _symbolFactory;
    private ISymbolRenderer _pointSymbolRenderer = new PointSymbolRenderer();
    private ISymbolRenderer _iconPointSymbolRenderer = new IconPointSymbolRenderer();

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

    public ILayerRenderer CreateVectorFillLayer(ILayerStyle style, 
        EvaluationContext context, 
        IEnumerable<IFeature> features)
    {
        // A filled area consits of a polygon, that is filled and a outline around it
        // Both need different vertices. So we need two types of vertices.
        
        if (!_paints.ContainsKey(style.Name))
        {
            return null;
        }

        var fillBuilder = new VertexBuilder();
        var outlineBuilder = new VertexBuilder();
        var paint = _paints[style.Name];

        // Create vertices for features that belong to a fill style (only fill area)
        foreach (var feature in features!)
        {
            feature.ToVertices(fillBuilder, paint.StrokeJoin);
        }

        if (paint.IsAntialias && paint.OutlineColor != paint.Color)
        {
            // Create vertices for features that belong to a outline style (only stroke around area)
            foreach (var feature in features!)
            {
                feature.ToVertices(outlineBuilder, 1f,
                    paint.StrokeJoin);
            }

            return new FillLayerRenderer(fillBuilder.Build(), outlineBuilder.Build(), true, _paints[style.Name]);
        }

        return new FillLayerRenderer(fillBuilder.Build(), true, _paints[style.Name]);
    }

    public ILayerRenderer CreateVectorLineLayer(ILayerStyle style, 
        EvaluationContext context, 
        IEnumerable<IFeature> features)
    {
        if (!_paints.ContainsKey(style.Name))
        {
            return null;
        }

        // Calc number of possible vertices
        var numOfPoints = 0;
        
        foreach (var feature in features!)
        {
            numOfPoints += feature.Geometry.NumPoints;
        }

        var builder = new VertexBuilder(numOfPoints * 3);
        var paint = _paints[style.Name];
        var maxStrokeWidth = paint.GetMaxStrokeWidth(context) + 2;  // Small adjustments for AA

        // Create vertices for features that belong to a line style (only the line, no border)
        foreach (var feature in features!)
        {
            feature.ToVertices(builder, maxStrokeWidth ,
                paint.StrokeCap,
                paint.StrokeCap, 
                paint.StrokeJoin);
        }

        return new LineLayerRenderer(builder.Build(), false, _paints[style.Name], maxStrokeWidth);
    }

    public ISymbol? CreateSymbol(Tile tile, 
        ILayerStyle style, 
        EvaluationContext context, 
        IFeature feature)
    {
        return _symbolFactory.CreateSymbol(tile, style, context, feature);
    }
}
