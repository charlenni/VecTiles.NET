using SkiaSharp;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Renderers.Common.Interfaces;
using VecTiles.Renderers.Skia.Extensions;
using VecTiles.Renderers.Skia.Primitives.Vertices;

namespace VecTiles.Renderers.Skia;

public class LineLayerRenderer : ILayerRenderer, IDisposable
{
    private readonly VertexBuffer _lineBuffer;
    private readonly bool _clip;
    private readonly IPaint _paint;
    private readonly LineShader? _shader;

    public LineLayerRenderer(VertexBuffer lineBuffer, bool clip, IPaint paint, float maxStrokeWidth)
    {
        _lineBuffer = lineBuffer;
        _clip = clip;
        _paint = paint;

        if (_paint.Pattern is not null)
        {
            return;
        }
        
        _shader = LineShader.Create(maxStrokeWidth);
    }

    public void Draw(object canvas, EvaluationContext context)
    {
        if (canvas is not SKCanvas skCanvas)
        { 
            return;
        }

        if (_shader is null)
        {
            return;
        }

        // Update values for paint regarding context
        var skPaints = _paint.ToSKPaint(context);

        skCanvas.Save();

        /*
        if (_clip)
        { 
            skCanvas.ClipPath(path); 
        }
        */

        // Dash period follows the SETTLED zoom level: width evaluated at the
        // integer zoom, converted to the tile px of that same level. During a
        // zoom animation this value is constant -> the pattern stays glued to
        // the geometry; at a level change it snaps discretely (dashes grow),
        // which matches the "everything gets bigger, nothing crawls" behavior.
        float scaleXInt = 1f; //ScaleForZoom(zInt);             // your zoom->scale mapping at zInt
        float dashRefHalfWidth = _paint.GetStrokeWidthFromIntZoom(context) / scaleXInt;

        // Draw features that belong to a line style
        foreach (var skPaint in skPaints)
        {
            skPaint.Shader = _shader.Update(skPaint.StrokeWidth, 
                skPaint.Color, 
                _paint.DashArray, 
                dashRefHalfWidth);
            skCanvas.DrawVertices(_lineBuffer.Vertices, SKBlendMode.SrcOver, skPaint);
        }

        skCanvas.Restore();
    }

    public void Dispose()
    {
        _lineBuffer?.Dispose();
        _shader?.Dispose();
    }
}
