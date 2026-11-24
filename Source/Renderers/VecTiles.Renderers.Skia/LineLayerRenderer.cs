using SkiaSharp;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Renderers.Common.Interfaces;
using VecTiles.Renderers.Skia.Extensions;
using VecTiles.Renderers.Skia.Primitives.Vertices;

namespace VecTiles.Renderers.Skia;

/// <summary>
/// Draws the stroked lines of a line layer as expanded tessellated line
/// geometry, supporting dash arrays and image patterns.
/// </summary>
public class LineLayerRenderer : ILayerRenderer, IDisposable
{
    private readonly VertexBuffer _lineBuffer;
    private readonly bool _clip;
    private readonly IPaint _paint;
    private readonly SKPaint _skPaint = new() { IsAntialias = true };
    private readonly LineShader? _shader;

    /// <summary>
    /// Create a line layer renderer for the given line geometry and paint.
    /// </summary>
    /// <param name="lineBuffer">Vertex buffer with the tessellated line geometry.</param>
    /// <param name="clip">Whether to clip the drawing to the tile bounds.</param>
    /// <param name="paint">Line paint evaluated at draw time.</param>
    /// <param name="maxStrokeWidth">Largest stroke width the vertex buffer was expanded for.</param>
    public LineLayerRenderer(VertexBuffer lineBuffer, bool clip, IPaint paint, float maxStrokeWidth)
    {
        _lineBuffer = lineBuffer;
        _clip = clip;
        _paint = paint;

        _shader = LineShader.Create(maxStrokeWidth);
    }

    /// <inheritdoc />
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
        _paint.Update(context);

        skCanvas.Save();

        /*
        if (_clip)
        { 
            skCanvas.ClipPath(path); 
        }
        */

        // Draw features that belong to a line style.
        // A pattern overrides dash array and color (Mapbox spec). The dash
        // array arrives in tile-space px (converters scale their style units
        // themselves), so no further stroke width multiplication happens.
        _skPaint.Shader = _shader.Update(_paint.StrokeWidth,
            _paint.Color.ToSKColor(),
            _paint.DashArray,
            pattern: _paint.Pattern);
        // An empty buffer has no native vertices (SKVertices.CreateCopy returns null for
        // empty arrays) and nothing to draw
        if (_lineBuffer.Vertices is { } lineVertices)
        {
            skCanvas.DrawVertices(lineVertices, SKBlendMode.SrcOver, _skPaint);
        }

        skCanvas.Restore();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _lineBuffer?.Dispose();
        _shader?.Dispose();
    }
}
