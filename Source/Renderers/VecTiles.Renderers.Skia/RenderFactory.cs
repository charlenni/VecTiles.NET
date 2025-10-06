using NetTopologySuite.Features;
using SkiaSharp;
using VecTiles.Common.Interfaces;
using VecTiles.Renderers.Common.Interfaces;
using VecTiles.Renderers.Skia.Extensions;

namespace VecTiles.Renderers.Skia;

public class RenderFactory : IRenderFactory
{
    static readonly SKRect _tileRect = new SKRect(0, 0, 512, 512);

    Dictionary<string, IPaint> _paints;
    ISymbolFactory _symbolFactory;

    public RenderFactory(IEnumerable<ILayerStyle> styles, IPaintFactory paintFactory, ISymbolFactory symbolFactory)
    {
        _symbolFactory = symbolFactory;
        _paints = new Dictionary<string, IPaint>(styles.Count());

        // Create for each style a IPaint, which then creates a SKPaint for a given evaluation context
        foreach (var style in styles)
        {
            var paint = paintFactory.CreatePaint(style);

            if (paint != null)
            {
                _paints.Add(style.Name, paint);
            }
        }
    }

    public IStyledLayer CreateBackgroundLayer(ILayerStyle style)
    {
        return new BackgroundLayer(_tileRect, _paints[style.Name]);
    }

    public IStyledLayer CreateRasterLayer(ILayerStyle style, byte[] data)
    {
        using var bitmap = SKBitmap.Decode(data);

        if (bitmap == null)
        {
            throw new Exception("Not possible to decode image");
        }

        return new RasterLayer(_tileRect, _paints[style.Name], bitmap);
    }

    public IStyledLayer CreateVectorFillLayer(ILayerStyle style, IEnumerable<IFeature> features)
    {
        var paths = new List<SKPath>(features!.Count());

        // Draw features that belong to a fill style (draw path by path)
        foreach (var feature in features!)
        {
            var path = feature.ToSKPath();

            paths.Add(path);
        }

        return new VectorLayer(paths, true, _paints[style.Name]);
    }

    public IStyledLayer CreateVectorLineLayer(ILayerStyle style, IEnumerable<IFeature> features)
    {
        var path = new SKPath();

        // Draw features that belong to a line style (add path by path and draw them at the end together)
        foreach (var feature in features!)
        {
            path.AddPath(feature.ToSKPath());
        }

        return new VectorLayer([path], false, _paints[style.Name]);
    }
}
