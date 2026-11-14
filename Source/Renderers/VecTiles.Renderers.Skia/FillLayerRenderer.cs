using SkiaSharp;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Renderers.Common.Interfaces;
using VecTiles.Renderers.Skia.Extensions;
using VecTiles.Renderers.Skia.Primitives.Vertices;

namespace VecTiles.Renderers.Skia;

public class FillLayerRenderer : ILayerRenderer, IDisposable
{
    private readonly VertexBuffer _fillBuffer;
    private readonly VertexBuffer? _lineBuffer;
    private readonly bool _clip;
    private readonly IPaint _paint;
    private readonly FillShader? _fillShader = FillShader.Create();
    private readonly LineShader? _lineShader = LineShader.Create(1f);

    public FillLayerRenderer(VertexBuffer fillBuffer, bool clip, IPaint paint) : this(fillBuffer, null, clip, paint)
    {
    }
    
    public FillLayerRenderer(VertexBuffer fillBuffer, VertexBuffer? lineBuffer, bool clip, IPaint paint)
    {
        _fillBuffer = fillBuffer;
        _lineBuffer = lineBuffer;
        _clip = clip;
        _paint = paint;
    }
    
    public void Draw(object canvas, EvaluationContext context)
    {
        if (canvas is not SKCanvas skCanvas)
        { 
            return;
        }

        if (_fillShader is null)
        {
            return;
        }

        // Update values for paint regarding context
        var skPaints = _paint.ToSKPaint(context);

        skCanvas.Save();

        /*if (_clip)
        { 
            skCanvas.ClipPath(path); 
        }*/

        // Draw features that belong to a fill style

        var i = 0;
        
        foreach (var skPaint in skPaints)
        {
            if (i == 0)
            {
                skPaint.Shader = _paint.Pattern is not null
                    ? _fillShader.Update(_paint.Pattern, true)
                    : _fillShader.Update(skPaint.Color);
                
                skPaint.Color = SKColors.White; // If we don't do this, we use alpha twice
                
                skCanvas.DrawVertices(_fillBuffer.Vertices, SKBlendMode.SrcOver, skPaint);
            }

            if (i == 1 && _lineBuffer is not null)
            {
                skPaint.Shader = _lineShader?.Update(1f, skPaint.Color);
                skPaint.Color = SKColors.White;

                skCanvas.DrawVertices(_lineBuffer.Vertices, SKBlendMode.SrcOver, skPaint);
            }

            i++;
        }
        
        skCanvas.Restore();
    }

    public void Dispose()
    {
        _fillBuffer?.Dispose();
        _lineBuffer?.Dispose();
        _fillShader?.Dispose();
        _lineShader?.Dispose();
    }
}
