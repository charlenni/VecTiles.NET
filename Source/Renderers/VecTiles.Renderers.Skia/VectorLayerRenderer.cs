using SkiaSharp;
using VecTiles.Renderers.Skia.Extensions;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Renderers.Common.Interfaces;

namespace VecTiles.Renderers.Skia;

public class VectorLayerRenderer : ILayerRenderer
{
    readonly IEnumerable<SKPath> _paths;
    readonly bool _clip;
    readonly IPaint _paint;

    public VectorLayerRenderer(IEnumerable<SKPath> paths, bool clip, IPaint paint)
    {
        _paths = paths;
        _clip = clip;
        _paint = paint;
    }

    public void Draw(object canvas, EvaluationContext context)
    {
        if (canvas is not SKCanvas skCanvas)
        { 
            return;
        }

        var skPaints = _paint.ToSKPaint(context);

        // Draw features that belong to a fill style (draw path by path)
        foreach (var path in _paths!)
        {
            skCanvas.Save();

            if (_clip)
            { 
                skCanvas.ClipPath(path); 
            }

            foreach (var skPaint in skPaints)
            {
                skCanvas.DrawPath(path, skPaint);
            }

            skCanvas.Restore();
        }
    }
}
