using SkiaSharp;
using Xunit.Abstractions;
using VecTiles.Common.Primitives;
using VecTiles.Renderers.Skia.Primitives.Vertices;

namespace VecTiles.Renderers.Skia.Test;

/// <summary>
/// SKVertices.CreateCopy returns null for empty vertex arrays; an empty VertexBuffer must
/// therefore carry no native vertices and the layer renderers must skip it instead of
/// passing null to DrawVertices (ArgumentNullException 'vertices', seen at high overzoom
/// levels where clipped polygons degenerate to empty buffers).
/// </summary>
public class VertexBufferEmptyTests(ITestOutputHelper output)
{
    [Fact]
    public void CreateCopyOfEmptyArraysReturnsNull()
    {
        var empty = SKVertices.CreateCopy(SKVertexMode.Triangles,
            new SKPoint[0], new SKPoint[0], new SKColor[0]);
        output.WriteLine($"SKVertices.CreateCopy(empty) = {(empty == null ? "null" : "valid")}");
        Assert.Null(empty);
    }

    [Fact]
    public void EmptyBufferHasNoNativeVertices()
    {
        var buffer = new VertexBuffer(new SKPoint[0], new SKPoint[0]);
        Assert.Null(buffer.Vertices);
        Assert.True(buffer.IsEmpty);
    }

    [Fact]
    public void DrawWithEmptyFillBufferDoesNotThrow()
    {
        var paint = new Paint("probe");
        paint.SetFixColor(Colors.Red);
        var fillRenderer = new FillLayerRenderer(
            new VertexBuffer(new SKPoint[0], new SKPoint[0]), clip: false, paint);

        using var bitmap = new SKBitmap(8, 8);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        fillRenderer.Draw(canvas, new EvaluationContext(19, 1f, 0f));
        canvas.Flush();
    }

    [Fact]
    public void DrawWithEmptyLineBufferDoesNotThrow()
    {
        var paint = new Paint("probe");
        paint.SetFixColor(Colors.Red);
        var lineRenderer = new LineLayerRenderer(
            new VertexBuffer(new SKPoint[0], new SKPoint[0]), clip: false, paint, maxStrokeWidth: 1f);

        using var bitmap = new SKBitmap(8, 8);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        lineRenderer.Draw(canvas, new EvaluationContext(19, 1f, 0f));
        canvas.Flush();
    }
}
