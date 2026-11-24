using SkiaSharp;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Renderers.Common.Interfaces;
using VecTiles.Renderers.Skia.Extensions;
using VecTiles.Renderers.Skia.Primitives.Vertices;

namespace VecTiles.Renderers.Skia;

/// <summary>
/// Draws the filled polygons of a fill layer as tessellated vertices and,
/// when present, the polygon outline as a line vertex buffer.
/// </summary>
public class FillLayerRenderer : ILayerRenderer, IDisposable
{
    private readonly VertexBuffer _fillBuffer;
    private readonly VertexBuffer? _lineBuffer;
    private readonly bool _clip;
    private readonly IPaint _paint;
    private readonly SKPaint _skPaint = new() { IsAntialias = true };
    private readonly FillShader? _fillShader = FillShader.Create();
    private readonly LineShader? _lineShader = LineShader.Create(1f);

    /// <summary>
    /// Create a fill layer renderer without polygon outline.
    /// </summary>
    /// <param name="fillBuffer">Vertex buffer with the tessellated fill geometry.</param>
    /// <param name="clip">Whether to clip the drawing to the tile bounds.</param>
    /// <param name="paint">Fill paint evaluated at draw time.</param>
    public FillLayerRenderer(VertexBuffer fillBuffer, bool clip, IPaint paint) : this(fillBuffer, null, clip, paint)
    {
    }
    
    /// <summary>
    /// Create a fill layer renderer with polygon outline.
    /// </summary>
    /// <param name="fillBuffer">Vertex buffer with the tessellated fill geometry.</param>
    /// <param name="lineBuffer">Vertex buffer with the outline geometry, or null.</param>
    /// <param name="clip">Whether to clip the drawing to the tile bounds.</param>
    /// <param name="paint">Fill paint evaluated at draw time.</param>
    public FillLayerRenderer(VertexBuffer fillBuffer, VertexBuffer? lineBuffer, bool clip, IPaint paint)
    {
        _fillBuffer = fillBuffer;
        _lineBuffer = lineBuffer;
        _clip = clip;
        _paint = paint;
    }
    
    /// <inheritdoc />
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

        // An empty buffer has no native vertices (SKVertices.CreateCopy returns null for
        // empty arrays) and nothing to draw: e.g. a polygon that degenerated while it was
        // clipped to the tile of a high overzoom level
        if (_fillBuffer.Vertices is { } fillVertices)
        {
            skCanvas.DrawVertices(fillVertices, SKBlendMode.SrcOver, _skPaint);
        }

        if (_paint.OutlineColor != _paint.Color && _lineBuffer is not null && _lineBuffer.Vertices is { } lineVertices)
        {
            _skPaint.Shader = _lineShader?.Update(_paint.StrokeWidth, _paint.OutlineColor.ToSKColor());
            _skPaint.Color = SKColors.White;

            skCanvas.DrawVertices(lineVertices, SKBlendMode.SrcOver, _skPaint);
        }
        
        skCanvas.Restore();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _fillBuffer?.Dispose();
        _lineBuffer?.Dispose();
        _fillShader?.Dispose();
        _lineShader?.Dispose();
    }
}
