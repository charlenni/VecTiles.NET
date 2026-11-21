using System.Drawing;
using NetTopologySuite.Geometries;
using SkiaSharp;
using VecTiles.Common.Enums;

namespace VecTiles.Renderers.Skia.Primitives.Vertices;

/// <summary>
/// Accumulates vertex positions and UVs and builds vertex buffers for them.
/// Fill and line tessellation append their geometry here.
/// </summary>
public class VertexBuilder
{
    /// <summary>
    /// Vertex positions as a list of SKPoint.
    /// </summary>
    private readonly List<SKPoint> _pos;

    /// <summary>
    /// UVs as list of SKPoint:
    /// Lines: u = signed distance to the centerline / maxHalfWidth
    ///        (square caps bake the box metric max(|lat|, along) into u),
    ///        v = cumulative arc length along the polyline (dash input).
    /// Fills: u = x, v = y (world position).
    /// </summary>
    private readonly List<SKPoint> _uv;

    /// <summary>
    /// Create an empty vertex builder.
    /// </summary>
    public VertexBuilder()
    {
        _pos = new();
        _uv = new();
    }

    /// <summary>
    /// Create a vertex builder with initial capacity for its lists.
    /// </summary>
    /// <param name="size">Initial capacity in vertices.</param>
    public VertexBuilder(int size)
    {
        _pos = new(size);
        _uv = new(size);
    }

    /// <summary>
    /// Ensures, that internal lists have enough space
    /// </summary>
    /// <param name="vertices">Number of maximal space</param>
    /// <returns>Current VertexBuilder</returns>
    public VertexBuilder EnsureCapacity(int vertices)
    {
        _pos.EnsureCapacity(vertices);
        _uv.EnsureCapacity(vertices);
        return this;
    }
    
    /// <summary>
    /// Build VertexBuffer for current lists
    /// </summary>
    /// <returns>A new VertexBuffer with converted SKVertices</returns>
    public VertexBuffer Build()
    {
        var buffer = new VertexBuffer([.. _pos], [.. _uv]);
        _pos.Clear();
        _uv.Clear();
        return buffer;
    }
    
    // --------------------------------------------------------------------
    //  Lines
    // --------------------------------------------------------------------
    /// <param name="lines">List of open lines (each with >= 2 points).</param>
    /// <param name="maxWidth">
    ///   Maximum width ever needed. The tessellation reserves this
    ///   amount; the shader cuts out the CURRENT width. The current
    ///   width must be <= maxWidth, otherwise the line is clipped
    ///   (not a render error, it just appears thinner).
    /// </param>
    /// <param name="startCap">Cap style at the start of each polyline.</param>
    /// <param name="endCap">Cap style at the end of each polyline.</param>
    /// <param name="join">Join style at interior corners: Miter, Bevel or Round.</param>
    /// <param name="miterLimit">
    ///   Only for SKStrokeJoin.Miter: maximum ratio miter length / halfWidth
    ///   (Skia default: 4). If exceeded (very sharp angles up to ~180
    ///   degrees) the join automatically falls back to Bevel -- no spikes.
    /// </param>
    public void AddLines(
        IReadOnlyList<Coordinate[]> lines,
        float maxWidth,
        StrokeCap startCap = StrokeCap.Round,
        StrokeCap endCap = StrokeCap.Round,
        StrokeJoin join = StrokeJoin.Round,
        float miterLimit = 4f)
    {
        var maxHalfWidth = maxWidth / 2f;

        if (maxHalfWidth <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(maxWidth),
                "Must be > 0 -- the tessellation reserves this width.");
        }

        foreach (var line in lines)
        {
            var pts = Tessellation.CompactToPoints(line);
            if (pts.Length < 2) continue;
            Tessellation.TessellatePolyline(pts, maxHalfWidth, startCap, endCap,
                join, miterLimit, _pos, _uv);
        }
    }

    // --------------------------------------------------------------------
    //  Filled polygons, world UVs (solid OR textured -- decided at DRAW time)
    // --------------------------------------------------------------------
    /// <summary>
    /// Triangulates a filled polygon with optional holes. Every vertex
    /// carries its WORLD position as UV (u = x, v = y).
    ///
    /// ONE mesh serves BOTH PolygonRenderer.Draw overloads: the SOLID
    /// shader ignores the UVs entirely, and the TEXTURE shader maps the
    /// world position to the atlas sprite with uniforms (pattern size,
    /// repeat and alpha are all runtime-changeable without re-triangulating).
    /// Switching solid <-> textured per frame costs nothing.
    ///
    /// Repeat semantics (evaluated in the shader):
    ///   repeat = true :  world position tiles every patternWidth x patternHeight
    ///   repeat = false:  the sprite is STRETCHED once over the polygon
    ///                    (world origin = sprite top-left).
    /// </summary>
    public void AddFill(
        Coordinate[] outer,
        IReadOnlyList<Coordinate[]>? holes = null)
    {
        Tessellation.TessellatePolygon(outer, holes, worldUvs: true, _pos, _uv);
    }

    // --------------------------------------------------------------------
    //  Closed polygons (stroked rings)
    // --------------------------------------------------------------------
    /// <summary>
    /// Tessellates CLOSED polygons (rings) as strokes. Every vertex gets a
    /// join -- including the closing seam between the last and the first
    /// segment. Caps are not applicable and therefore ignored.
    /// 
    /// Implementation note: this simply appends the first point once at the
    /// end and runs the regular pipeline with caps disabled. The join at the
    /// duplicated seam point then sees the normal of the LAST segment (back
    /// to the start) and the normal of the FIRST segment -- producing a
    /// correct join at the seam automatically, for all three join styles.
    /// </summary>
    /// <param name="rings">Closed rings (each with >= 2 distinct points;
    /// the closing edge from the last point back to the first is implied).</param>
    /// <param name="maxHalfWidth">Maximum width for creating vertices</param>
    /// <param name="join">Type of join between lines</param>
    /// <param name="miterLimit">Miter limit for miter joins</param>
    public void AddOutline(
        IReadOnlyList<Coordinate[]> rings,
        float maxWidth,
        StrokeJoin join = StrokeJoin.Round,
        float miterLimit = 4f)
    {
        var maxHalfWidth = maxWidth / 2f;

        if (maxHalfWidth <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(maxWidth),
                "Must be > 0 -- the tessellation reserves this width.");
        }

        foreach (var ring in rings)
        {
            var open = Tessellation.CompactToPoints(ring);
            if (open.Length < 2) continue;

            // Strip a redundant closing point (first == last, common in
            // GeoJSON/WKT data): the pipeline appends the first point
            // itself below -- otherwise a zero-length segment would
            // degenerate the seam join.
            int distinct = open.Length;
            if (distinct > 1 &&
                Math.Abs(open[0].X - open[distinct - 1].X) < float.Epsilon &&
                Math.Abs(open[0].Y - open[distinct - 1].Y) < float.Epsilon)
            {
                distinct--;
            }

            if (distinct < 2) continue;

            // Append the first point to close the ring; the regular
            // pipeline then produces n segments. The SEAM join (between
            // the last and the first segment, at the start/end point) is
            // emitted by TessellatePolyline itself via closedRing: true
            // -- the interior join loop alone would leave a gap there.
            var closed = new PointF[distinct + 1];
            Array.Copy(open, closed, distinct);
            closed[^1] = open[0];

            Tessellation.TessellatePolyline(closed, maxHalfWidth,
                StrokeCap.Butt, StrokeCap.Butt, // no caps on rings
                join, miterLimit, _pos, _uv, true);
        }
    }
}