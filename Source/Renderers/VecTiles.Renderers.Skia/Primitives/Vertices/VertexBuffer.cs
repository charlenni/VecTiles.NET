using SkiaSharp;

namespace VecTiles.Renderers.Skia.Primitives.Vertices;

/// <summary>
/// A VertexBuffer holding the native format for a group of vertices
/// </summary>
public sealed class VertexBuffer : IDisposable
{
    /// <summary>
    /// Empty VertexBuffer
    /// </summary>
    /// <remarks>
    /// This is shared, don't dispose it outside
    /// </remarks>
    public static readonly SKVertices Empty = SKVertices.CreateCopy(SKVertexMode.Triangles, new SKPoint[0], new SKPoint[0], new SKColor[0]);

    /// <summary>
    /// Create a new VertexBuffer from points, texs and colors.
    /// All the data is converted to a SKVertices and saved for later use.
    /// </summary>
    /// <remarks>
    /// Has to be disposed later
    /// </remarks>
    /// <param name="points">Points to use for the vertices. Always 3 points for one triangle</param>
    /// <param name="texs">Tex coordinates for texture</param>
    /// <param name="colors">Colors to use</param>
    /// <exception cref="ArgumentException">Throws an exception if length of arrays is different</exception>
    public VertexBuffer(SKPoint[] points, SKPoint[] texs, SKColor[]? colors = null)
    {
        if (points.Length != texs.Length)
            throw new ArgumentException(
                $"Position/UV count mismatch: {points.Length} vs {texs.Length}");
        
        if (colors is not null && points.Length != colors.Length)
            throw new ArgumentException(
                $"Position/Colors count mismatch: {points.Length} vs {colors.Length}");

        _vertices = points.Length == 0
            ? null
            : SKVertices.CreateCopy(SKVertexMode.Triangles, points, texs, colors);
    }

    private SKVertices? _vertices;

    public SKVertices Vertices => _vertices ?? Empty;

    public void Dispose()
    {
        _vertices?.Dispose();
        _vertices = null;
    }
}