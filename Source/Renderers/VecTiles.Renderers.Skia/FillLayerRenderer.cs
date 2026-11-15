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
    private readonly SKPaint _skPaint = new() { IsAntialias = true };
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
        _paint.Update(context);

        skCanvas.Save();

        /*if (_clip)
        { 
            skCanvas.ClipPath(path); 
        }*/

        // Draw features that belong to a fill style

        _skPaint.Shader = _paint.Pattern is not null
            ? _fillShader.Update(_paint.Pattern, true)
            : _fillShader.Update(_paint.Color.ToSKColor());
                
        _skPaint.Color = SKColors.White; // If we don't do this, we use alpha twice
                
        skCanvas.DrawVertices(_fillBuffer.Vertices, SKBlendMode.SrcOver, _skPaint);

        if (_paint.OutlineColor != _paint.Color && _lineBuffer is not null)
        {
            _skPaint.Shader = _lineShader?.Update(1f, _paint.OutlineColor.ToSKColor());
            _skPaint.Color = SKColors.White;

            skCanvas.DrawVertices(_lineBuffer.Vertices, SKBlendMode.SrcOver, _skPaint);
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
