// ============================================================================
//  VertexMesh -- ONE-TIME tessellation of lines & polygon fills
//  ----------------------------------------------------------------------------
//  Builds flat position + UV arrays ready for SKVertices.CreateCopy.
//  The RENDERERS (LineRenderer, PolygonRenderer) own no geometry: they take
//  the SKVertices per draw call and drive everything visual via uniforms.
//
//  Vertex UV semantics (shared by all builders):
//    Lines:  u = signed distance to the stroke centerline / maxHalfWidth
//            (square caps bake the box metric max(|lat|, along) into u),
//            v = cumulative arc length along the polyline (dash input)
//    Fills:  u = x, v = y (WORLD position -- the texture shader maps it to
//            the atlas; the solid shader ignores UVs entirely, so ONE mesh
//            serves BOTH PolygonRenderer.Draw overloads)
//
//  All outer edges are antialiased: every line triangle carries per-vertex
//  distance values (u), so the shader can feather the edge via smoothstep.
//
//  Usage:
//    var mesh     = VertexMesh.AddLines(polylines, maxHalfWidth: 8f,
//                     SKStrokeCap.Round, SKStrokeCap.Round,
//                     SKStrokeJoin.Round, miterLimit: 4f);
//    var vertices = SKVertices.CreateCopy(SKVertexMode.Triangles,
//                     mesh.Positions, mesh.UVs, colors: null);   // upload ONCE
//    using var r  = new LineRenderer(maxHalfWidth: 8f);
//    // per frame (uniforms only):
//    r.Draw(canvas, vertices, halfWidth: 3f, SKColors.Red);            // solid
//    r.Draw(canvas, vertices, 3f, SKColors.Red, dashOn: 10, dashOff: 6);
// ============================================================================

using System.Buffers;
using System.Drawing;
using EarcutDotNet;
using NetTopologySuite.Geometries;
using SkiaSharp;
using VecTiles.Common.Enums;

namespace VecTiles.Renderers.Skia.Primitives.Vertices;

// ============================================================================
// 1) Tessellation: polylines / polygons -> triangles with UVs
// ============================================================================
/// <summary>
/// Tessellation of polylines and polygons into triangles with UVs. Line
/// vertices carry signed distance and arc length in the UVs so stroke width,
/// dashes and patterns are controlled by the shader at draw time.
/// </summary>
public static class Tessellation
{
    // --------------------------------------------------------------------
    //  Shared polygon triangulation (solid u=0 OR world UVs)
    /// <summary>
    /// Triangulates a polygon with optional hole rings using earcut and appends
    /// the triangles to the vertex lists. Holes must follow their outer ring in
    /// ring order (outer ring first).
    /// </summary>
    /// <param name="outer">Coordinates of the outer ring.</param>
    /// <param name="holes">Hole rings, or null.</param>
    /// <param name="worldUvs">When true, UVs carry the world position (u = x, v = y) for textured fills; otherwise UVs are left at zero for solid fills.</param>
    /// <param name="pos">List the vertex positions are appended to.</param>
    /// <param name="uv">List the vertex UVs are appended to.</param>
    // --------------------------------------------------------------------
    public static void TessellatePolygon(
        Coordinate[] outer,
        IReadOnlyList<Coordinate[]>? holes, bool worldUvs,
        List<SKPoint> pos, List<SKPoint> uv)
    {
        // Collect all rings into one flat coordinate list:
        // outer ring first, then all hole rings back to back.
        
        var numOfCoords = CalcNumOfCoords(outer, holes);
        var array = ArrayPool<double>.Shared.Rent(numOfCoords * 2);

        try
        {
            var coords = array.AsSpan(0, numOfCoords * 2);
            var offset = 0;
            
            var ringStartIndices = new List<int>();

            void AppendRing(Coordinate[] ring, Span<double> coords)
            {
                var length = ring.Length;
                // Remove last == first coordinate
                if (Math.Abs(ring[0].X - ring[^1].X) < double.Epsilon &&  Math.Abs(ring[0].Y - ring[^1].Y) < double.Epsilon)
                    length--;
                // Check for at least 3 coords for a area
                if (length < 3)
                    return;
                //ring = SanitizeRing(ring);
                ringStartIndices.Add(offset >> 1);
                for (var i = 0; i < length; i++)
                {
                    coords[offset++] = ring[i].X;
                    coords[offset++] = ring[i].Y;
                }
            }

            AppendRing(outer, coords);
            if (holes is { Count: > 0 })
                foreach (var hole in holes) AppendRing(hole, coords);

            // The first ring is the outline; every following ring is a hole.
            // Vertex indices returned by the triangulator refer to the flat
            // coordinate list (x = coords[i * 2], y = coords[i * 2 + 1]).
            int[] indices = holes is { Count: > 0 }
                ? PolygonTriangulator.TriangulateWithHoles(coords, ringStartIndices)
                : PolygonTriangulator.Triangulate(coords);

            for (int i = 0; i + 2 < indices.Length; i += 3)
            {
                for (int corner = 0; corner < 3; corner++)
                {
                    int index = indices[i + corner];
                    float x = (float)coords[index * 2], y = (float)coords[index * 2 + 1];
                    if (worldUvs)
                    {
                        // World position AS texture coordinate; the texture
                        // shader does the atlas mapping (see PolygonRenderer).
                        pos.Add(new SKPoint(x, y));
                        uv.Add(new SKPoint(x, y));
                    }
                    else
                    {
                        // u = 0: solid fill in the LINE pipeline
                        // (dist = 0 -> coverage = 1; v = 0 -> no dash cut).
                        pos.Add(new SKPoint(x, y));
                        uv.Add(new SKPoint(0f, 0f));
                    }
                }
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(array);
        }
    }
    
    // --------------------------------------------------------------------
    //  Core: Tessellation of ONE polyline
    // --------------------------------------------------------------------
    /// <summary>
    /// Expands a polyline into two triangle strips (left and right of the
    /// centerline) with segment quads trimmed at the corner bisectors and
    /// joins and caps emitted either as miter triangles or fans.
    /// </summary>
    /// <param name="pts">Polyline points in order.</param>
    /// <param name="hw">Half of the stroke width in pixels.</param>
    /// <param name="startCap">Cap style at the start of the line.</param>
    /// <param name="endCap">Cap style at the end of the line.</param>
    /// <param name="join">Join style at interior corners.</param>
    /// <param name="miterLimit">Miter limit for miter joins.</param>
    /// <param name="pos">List the vertex positions are appended to.</param>
    /// <param name="uv">List the vertex UVs are appended to.</param>
    /// <param name="closedRing">When true, treats the polyline as a closed ring and emits the join at the closing seam.</param>
    public static void TessellatePolyline(
        PointF[] pts, float hw,
        StrokeCap startCap, StrokeCap endCap,
        StrokeJoin join, float miterLimit,
        List<SKPoint> pos, List<SKPoint> uv, bool closedRing = false)
    {
        int pointCount = pts.Length;

        // --- 1. Per-segment normals -----------------------------------
        //    n_i = perpendicular to (p_{i+1} - p_i), normalized to length 1
        var normals = new PointF[pointCount - 1];
        for (int i = 0; i < pointCount - 1; i++)
        {
            var d = new PointF(pts[i + 1].X - pts[i].X, pts[i + 1].Y - pts[i].Y);
            float len = MathF.Sqrt(d.X * d.X + d.Y * d.Y);
            if (len < 1e-6f) len = 1e-6f;               // degenerate: dummy normal
            normals[i] = new PointF(-d.Y / len, d.X / len);
        }

        // --- 1b. Cumulative arc length per point ------------------------
        //    The dash pattern is evaluated IN THE SHADER from the vertex
        //    v coordinate, which carries the arc length along the polyline
        //    (in pixels). cumLen[0] = 0. This makes the dash pattern a
        //    pure uniform: dash on/off lengths and phase can change at
        //    runtime WITHOUT re-tessellating the mesh.
        var cumLen = new float[pointCount];
        for (int i = 1; i < pointCount; i++)
        {
            float dx = pts[i].X - pts[i - 1].X;
            float dy = pts[i].Y - pts[i - 1].Y;
            cumLen[i] = cumLen[i - 1] + MathF.Sqrt(dx * dx + dy * dy);
        }

        // --- 2. Segment quads (trimmed at each corner's angle bisector) ---
        //    WITHOUT the trim, the two quads adjacent to a corner extend
        //    +-hw laterally past the corner and OVERLAP in a wedge on the
        //    INSIDE of the corner -> with a transparent stroke color,
        //    SrcOver composites that wedge TWICE (a bright dot in every
        //    corner; invisible with opaque colors). Fix: clip each quad at
        //    the angle bisector through the corner (the "inner miter",
        //    intersection of the two INNER offset edges). The trim edge
        //    corner<->innerMiter is the locus of equal distance to BOTH
        //    centerlines -> the u interpolation stays exactly linear
        //    there, antialiasing remains correct. Joins (outer side) are
        //    unaffected: they only share edges with the trimmed quads.
        //    The INNER side is derived from the neighbouring segment's
        //    DIRECTION (dot product with the own normal), NOT from the
        //    cross product of normals -> the trim is independent of the
        //    normal-handedness convention (works with left AND right
        //    normals; a flipped normal convention flips sigma and
        //    (nSelf+nOther) together, leaving the miter point unchanged).
        for (int i = 0; i < pointCount - 1; i++)
        {
            PointF segmentStart = pts[i], segmentEnd = pts[i + 1];
            PointF n = normals[i];
            PointF edgeOffset = Scale(n, hw);

            // Trim at the start/end corner? On closed rings also at the
            // seam (segment 0 start / last segment end): both trims use
            // the same segment pair -> they meet exactly, no gap, no
            // overlap.
            bool trimStart = i > 0 || (closedRing && pointCount >= 3);
            bool trimEnd   = i < pointCount - 2 || (closedRing && pointCount >= 3);
            PointF prevN = i > 0 ? normals[i - 1] : normals[pointCount - 2];
            PointF nextN = i < pointCount - 2 ? normals[i + 1] : normals[0];

            // Direction from the corner AWAY along the neighbouring
            // segment (unnormalized -- only its sign matters).
            PointF dirAwayPrev = PointF.Empty, dirAwayNext = PointF.Empty;
            if (trimStart)
            {
                PointF prevPoint = i > 0 ? pts[i - 1] : pts[pointCount - 2];
                dirAwayPrev = Sub(prevPoint, segmentStart);
            }
            if (trimEnd)
            {
                PointF nextPoint = i < pointCount - 2 ? pts[i + 2] : pts[1];
                dirAwayNext = Sub(nextPoint, segmentEnd);
            }

            float sSign = 0f, eSign = 0f;
            PointF sMiter = PointF.Empty, eMiter = PointF.Empty;
            bool startTrimmed = trimStart
                && InnerMiter(segmentStart, n, prevN, dirAwayPrev, hw,
                              out sSign, out sMiter);
            bool endTrimmed = trimEnd
                && InnerMiter(segmentEnd, n, nextN, dirAwayNext, hw,
                              out eSign, out eMiter);

            // Safety net: on very short segments (length ~ hw) the two
            // trims can cross -> fall back to the untrimmed quad (the
            // wedge is negligible compared to a self-intersection).
            if (startTrimmed && endTrimmed)
            {
                var d = Sub(segmentEnd, segmentStart);
                float len = MathF.Sqrt(d.X * d.X + d.Y * d.Y);
                if (len > 1e-6f)
                {
                    var dir = new PointF(d.X / len, d.Y / len);
                    float aS = (sMiter.X - segmentStart.X) * dir.X
                             + (sMiter.Y - segmentStart.Y) * dir.Y;
                    float aE = (eMiter.X - segmentStart.X) * dir.X
                             + (eMiter.Y - segmentStart.Y) * dir.Y;
                    if (aS > aE) startTrimmed = endTrimmed = false;
                }
            }

            // Start edge (point, u) in u-ascending order. The miter lies
            // on the own inner offset edge -> u = sigma (= +-1); the
            // corner has u = 0. All start points carry v = cumLen[i]
            // (same convention as joins/caps: points "belong" to their
            // corner).
            var startPts = new List<(PointF P, float U)>(3);
            if (startTrimmed)
            {
                if (sSign > 0f)     // inner side = +n: startL is outside
                {
                    startPts.Add((Sub(segmentStart, edgeOffset), -1f));
                    startPts.Add((segmentStart, 0f));
                    startPts.Add((sMiter, sSign));
                }
                else                // inner side = -n: startR is outside
                {
                    startPts.Add((sMiter, sSign));
                    startPts.Add((segmentStart, 0f));
                    startPts.Add((Add(segmentStart, edgeOffset), 1f));
                }
            }
            else
            {
                startPts.Add((Sub(segmentStart, edgeOffset), -1f));
                startPts.Add((Add(segmentStart, edgeOffset), 1f));
            }

            // End edge (point, u), also u-ascending (appended reversed
            // below), v = cumLen[i + 1].
            var endPts = new List<(PointF P, float U)>(3);
            if (endTrimmed)
            {
                if (eSign > 0f)
                {
                    endPts.Add((Sub(segmentEnd, edgeOffset), -1f));
                    endPts.Add((segmentEnd, 0f));
                    endPts.Add((eMiter, eSign));
                }
                else
                {
                    endPts.Add((eMiter, eSign));
                    endPts.Add((segmentEnd, 0f));
                    endPts.Add((Add(segmentEnd, edgeOffset), 1f));
                }
            }
            else
            {
                endPts.Add((Sub(segmentEnd, edgeOffset), -1f));
                endPts.Add((Add(segmentEnd, edgeOffset), 1f));
            }

            // v: the quad's OWN along coordinate for EVERY vertex, including
            // the trim-edge miters. Without this the trim edge would be a
            // frozen iso-v line: dash cuts would bend into the 45-degree
            // diagonal at corners (slanted dash ends, irregular dash
            // lengths). The v "crack" along the trim edge is intentional
            // (matches Skia's butt-cut dash behaviour at corners).
            var direct = Normalize(Sub(segmentEnd, segmentStart));
            var cycle = new List<(PointF P, float U, float V)>(6);
            foreach (var (p, u) in startPts)
                cycle.Add((p, u, cumLen[i] + Dot(Sub(p, segmentStart), direct)));
            for (int k = endPts.Count - 1; k >= 0; k--)
                cycle.Add((endPts[k].P, endPts[k].U,
                    cumLen[i] + Dot(Sub(endPts[k].P, segmentStart), direct)));
            // Fan-triangulate the convex polygon from the first vertex:
            for (int k = 1; k + 1 < cycle.Count; k++)
                AddDistanceTri(
                    cycle[0].P, cycle[0].U, cycle[k].P, cycle[k].U,
                    cycle[k + 1].P, cycle[k + 1].U,
                    cycle[0].V, cycle[k].V, cycle[k + 1].V, pos, uv);
        }
        
        // --- 3. Joins at interior points (OUTER side of the corner) ---
        //    Fills the gap between the two adjacent segment quads. The
        //    outer side follows from the sign of the cross product of the
        //    adjacent normals. ALL joins use distance-based u values
        //    (u = distance to the stroke centerline / maxHalfWidth) so the
        //    shader antialiases the outer edge:
        //      corner          -> u = 0
        //      outerPrev/Next  -> u = 1   (they sit at hw on the outer edge)
        //      miterPoint      -> u = 1   (intersection of BOTH outer edges,
        //                                    at distance hw from both
        //                                    centerlines)
        //      Round : wedge fan approximating a circular arc; the shader
        //              cuts out the current width with AA
        //      Bevel : single wedge triangle -> flat outer edge with AA
        //      Miter : two triangles leaning against each segment's
        //              centerline; falls back to Bevel when miterRatio
        //              exceeds miterLimit
        for (int i = 1; i < pointCount - 1; i++)
        {
            AddJoin(pts[i], normals[i - 1], normals[i], cumLen[i],
                hw, join, miterLimit, pos, uv);
        }

        // --- 3b. Seam join for CLOSED rings ---------------------------
        //    The join loop above only covers INTERIOR points (1..n-2).
        //    A closed ring's start/end point (index 0 == the appended
        //    last point) needs a join TOO: the last segment's normal is
        //    normals[n-2] (from the last original point back to the
        //    start), the first segment's normal is normals[0]. Without
        //    this, the ring has a BUTT-STYLE GAP at the seam, no matter
        //    which join style is selected.
        if (closedRing && pointCount >= 3)
        {
            AddJoin(pts[0], normals[pointCount - 2], normals[0], 0f,
                hw, join, miterLimit, pos, uv);
        }

        // --- 4. End caps ----------------------------------------------
        //    IMPORTANT: pass the direction pointing OUTWARD at BOTH ends.
        //    The round-cap fan sweeps from +side over +outward (the outer
        //    half-disc) to -side.
        if (startCap != StrokeCap.Butt)
        {
            var startOutward = new PointF(pts[0].X - pts[1].X,
                                          pts[0].Y - pts[1].Y);
            if (startCap == StrokeCap.Square)
                AddCapSquare(pts[0], startOutward, hw, cumLen[0], pos, uv);
            else
                AddCapRound(pts[0], startOutward, hw, arcLength: cumLen[0], pos, uv);
        }

        if (endCap == StrokeCap.Butt) return;

        var endOutward = new PointF(pts[pointCount - 1].X - pts[pointCount - 2].X,
                                    pts[pointCount - 1].Y - pts[pointCount - 2].Y);
        if (endCap == StrokeCap.Square)
            AddCapSquare(pts[pointCount - 1], endOutward, hw, cumLen[pointCount - 1], pos, uv);
        else
            AddCapRound(pts[pointCount - 1], endOutward, hw, arcLength: cumLen[pointCount - 1], pos, uv);
    }

    /// <summary>
    /// Converts NTS coordinates to a list of points, dropping the duplicated
    /// closing coordinate when the first and last point are equal.
    /// </summary>
    /// <param name="coords">Coordinates to convert.</param>
    /// <returns>The points of the coordinate list without a duplicated closing coordinate.</returns>
    public static PointF[] CompactToPoints(Coordinate[] coords)
    {
        if (coords is null)
            throw new ArgumentNullException(nameof(coords));

        var result = new List<PointF>(coords.Length);
        for (int i = 0; i < coords.Length; i++)
        {
            var p = new PointF((float)coords[i].X, (float)coords[i].Y);
            // Skip CONSECUTIVE DUPLICATE points: zero-length segments would
            // otherwise emit degenerate triangles (dummy normals, all
            // vertices identical) and degenerate the seam join of closed
            // rings. Single-point and all-identical inputs then collapse
            // to < 2 points and are skipped by the callers.
            if (result.Count > 0 && result[^1].X == p.X && result[^1].Y == p.Y)
                continue;
            // Drop (NEAR-)COLLINEAR middle points: if the previous point
            // lies on the SEGMENT from its own predecessor to the NEW
            // point, it carries no geometry -- remove it (cascading
            // while-loop). Segment test, NOT line test: points outside
            // the segment span (reversals/spikes) are REAL geometry.
            // Endpoints are never dropped: the first point has no
            // predecessor, the last point is the "new" p itself.
            while (result.Count >= 2 &&
                   DistanceToSegment(result[^1], result[^2], p)
                   <= CollinearEpsilon)
                result.RemoveAt(result.Count - 1);
            result.Add(p);
        }
        return [.. result];
    }

    // --------------------------------------------------------------------
    //  Vector helpers
    // --------------------------------------------------------------------
    static PointF Scale(PointF v, float s) => new(v.X * s, v.Y * s);
    static PointF Add(PointF a, PointF b) => new(a.X + b.X, a.Y + b.Y);
    static PointF Sub(PointF a, PointF b) => new(a.X - b.X, a.Y - b.Y);
    static PointF Normalize(PointF v)
    {
        float l = MathF.Sqrt(v.X * v.X + v.Y * v.Y);
        return l < 1e-6f ? new(0, 0) : new(v.X / l, v.Y / l);
    }
    static float Dot(PointF a, PointF b) => a.X * b.X + a.Y * b.Y;

    /// <summary>
    /// Intersection of the two INNER offset edges at a corner (the
    /// "inner miter") -- the point at which the segment quad is trimmed.
    /// Returns false for degenerate cases (neighbouring segment
    /// parallel to our own, or a ~180-degree turn): then the quad MUST
    /// stay untrimmed (no wedge, or trimming would cut into the stroke).
    ///
    /// Handedness-free: the inner side is derived from the neighbouring
    /// segment's DIRECTION (dot product with our own normal), not from
    /// the cross product of the two normals -- so the trim is correct
    /// regardless of whether normals are left-facing (n = (-dy, dx)) or
    /// right-facing (n = (dy, -dx)): a flipped convention flips BOTH
    /// sigma and (nSelf + nOther), leaving the miter point unchanged.
    ///
    /// The returned point lies on BOTH inner offset edges, i.e. exactly
    /// at u = sigma (= +-1) w.r.t. our own centerline; the edge
    /// corner &lt;-&gt; miter is the locus of equal distance to both
    /// centerlines, so the u interpolation is exactly linear there
    /// (antialiasing stays correct).
    /// </summary>
    /// <param name="corner">The corner point (start or end of OUR segment).</param>
    /// <param name="nSelf">Our segment's unit normal.</param>
    /// <param name="nOther">The neighbouring segment's unit normal.</param>
    /// <param name="dirAwayOther">
    /// UNNORMALIZED direction from the corner AWAY along the
    /// neighbouring segment (towards its far point). Only its
    /// orientation matters.
    /// </param>
    /// <param name="sigma">
    /// Sign of the INNER side w.r.t. our normal: the neighbouring
    /// segment bends towards sigma * nSelf.
    /// </param>
    private static bool InnerMiter(
        PointF corner, PointF nSelf, PointF nOther, PointF dirAwayOther,
        float hw, out float sigma, out PointF miter)
    {
        sigma = 0f;
        miter = corner;

        // Which side does the stroke turn to? The neighbouring segment
        // runs towards dirAwayOther; if it moves to +nSelf, the inside
        // of the corner is on +nSelf. Dot > 0 only if the neighbour
        // actually leaves the corner sideways (parallel continuation
        // gives dot ~ 0 -> untrimmed, no wedge).
        float dot = dirAwayOther.X * nSelf.X + dirAwayOther.Y * nSelf.Y;
        if (MathF.Abs(dot) < 1e-6f) return false;    // collinear: no wedge
        sigma = dot > 0f ? 1f : -1f;

        // Intersection of the two inner offset lines. On the inner side
        // both offset lines are at signed distance sigma*hw from their
        // centerlines, i.e. (m - corner)·nSelf = sigma*hw and
        // (m - corner)·nOther = sigma*hw. Ansatz m = corner + t*(nSelf+nOther):
        // t * (1 + nSelf·nOther) = sigma*hw  with
        // 1 + nSelf·nOther = |nSelf+nOther|^2 / 2  ->  t = 2*sigma*hw / |n|^2.
        float sumX = nSelf.X + nOther.X, sumY = nSelf.Y + nOther.Y;
        float sumSq = sumX * sumX + sumY * sumY;
        if (sumSq < 1e-4f) return false;            // ~180-degree turn

        float t = sigma * 2f * hw / sumSq;
        miter = new PointF(corner.X + t * sumX, corner.Y + t * sumY);
        return true;
    }
    
    // --------------------------------------------------------------------
    //  Emit helpers
    // --------------------------------------------------------------------
    
    // --------------------------------------------------------------------
    //  Join emission (used by the interior join loop AND the ring seam)
    // --------------------------------------------------------------------
    /// <summary>
    /// Emits ONE join at a corner: fills the gap between the two adjacent
    /// segment quads on the OUTER side (sign of the cross product of the
    /// adjacent normals). ALL joins use distance-based u values
    /// (u = distance to the stroke centerline / maxHalfWidth) so the
    /// shader antialiases the outer edge:
    ///      corner          -> u = 0
    ///      outerPrev/Next  -> u = 1   (they sit at hw on the outer edge)
    ///      miterPoint      -> u = 1   (intersection of BOTH outer edges,
    ///                                    at distance hw from both
    ///                                    centerlines)
    ///      Round : wedge fan approximating a circular arc; the shader
    ///              cuts out the current width with AA
    ///      Bevel : single wedge triangle -> flat outer edge with AA
    ///      Miter : two triangles leaning against each segment's
    ///              centerline; falls back to Bevel when miterRatio
    ///              exceeds miterLimit
    /// <paramref name="cornerArcLength"/> is the corner's cumulative arc
    /// length (dash phase); the ring seam passes 0 (the start point).
    /// </summary>
    /// <summary>
    /// UV offset that breaks the per-triangle position-&gt;UV degeneracy of
    /// join triangles (see EmitJoin). Joins have NO along-path extent --
    /// the whole join must share the corner's dash phase -- so unlike the
    /// round cap (which varies v semantically by its along displacement)
    /// the join keeps v constant UP TO this epsilon ladder. 0.01 px per
    /// arc step shifts the dash phase by at most ~0.2 px -- invisible --
    /// while making every triangle's UV mapping invertible for Skia's
    /// DrawVertices rasterizer, which otherwise silently DROPS the
    /// triangle (see the render tests: joins and caps rendered as nothing
    /// before this fix, with all geometry tests green).
    /// </summary>
    private const float UvEps = 0.01f;

    private static void AddJoin(
        PointF corner, PointF prevNormal, PointF nextNormal, float cornerArcLength,
        float hw, StrokeJoin join, float miterLimit,
        List<SKPoint> pos, List<SKPoint> uv)
    {
        float cross = prevNormal.X * nextNormal.Y
                    - prevNormal.Y * nextNormal.X;
        float sideSign = cross > 0f ? -1f : 1f;     // outer side of the corner
        PointF outerPrev = Add(corner, Scale(prevNormal, sideSign * hw));
        PointF outerNext = Add(corner, Scale(nextNormal, sideSign * hw));

        switch (join)
        {
            case StrokeJoin.Miter:
            {
                // Miter direction: normalized sum of the two normals.
                // Miter length (in hw units) = 2 / |n1 + n2|
                var normalSum = new PointF(prevNormal.X + nextNormal.X,
                                            prevNormal.Y + nextNormal.Y);
                float sumLen = MathF.Sqrt(normalSum.X * normalSum.X
                                        + normalSum.Y * normalSum.Y);
                float miterRatio = sumLen < 1e-4f
                    ? float.MaxValue                 // ~180-degree turn: no tip
                    : 2f / sumLen;

                if (miterRatio <= miterLimit)
                {
                    var miterDir = new PointF(normalSum.X / sumLen,
                                              normalSum.Y / sumLen);
                    PointF miterPoint = Add(corner,
                        Scale(miterDir, sideSign * hw * miterRatio));

                    // The miter point is the intersection of both outer
                    // edges, i.e. at distance hw from BOTH centerlines
                    // -> u = 1. Split the fill into TWO triangles, each
                    // leaning against ONE segment's centerline. In both
                    // triangles the linearly interpolated distance is
                    // exact (all edges lie on iso-lines or the
                    // centerline). A single triangle with
                    // u(miterPoint) = miterRatio would be cut off by
                    // the shader (it would look like Bevel).
                    // The +eps offsets on v keep every triangle's
                    // position->UV mapping invertible (see the UV note
                    // in the method comment).
                    AddDistanceTri(corner, 0f, outerPrev, 1f, miterPoint, 1f, cornerArcLength, cornerArcLength, cornerArcLength + UvEps, pos, uv);
                    AddDistanceTri(corner, 0f, miterPoint, 1f, outerNext, 1f, cornerArcLength, cornerArcLength + UvEps, cornerArcLength + 2 * UvEps, pos, uv);
                }
                else
                {
                    goto case StrokeJoin.Bevel;    // limit exceeded -> Bevel
                }
                break;
            }

            case StrokeJoin.Bevel:
                // Straight cut line between the two outer edge points.
                // outerNext gets v + eps: the two outer vertices must not
                // share IDENTICAL UVs, or the rasterizer drops the whole
                // triangle (degenerate position->UV mapping).
                AddDistanceTri(corner, 0f, outerPrev, 1f, outerNext, 1f, cornerArcLength, cornerArcLength, cornerArcLength + UvEps, pos, uv);
                break;

            default:    // SKStrokeJoin.Round
            {
                // Fan of triangles approximating the circular arc
                // (radius hw around the corner, swept between the two
                // outer edge directions). A SINGLE wedge triangle would
                // produce iso-lines parallel to the chord -> the shader
                // would cut a FLAT edge (Round would look like Bevel).
                // u = 0 at the corner, u = 1 at all arc points.
                // ~8 segments per 90 degrees look smooth.
                var dirPrev = Scale(prevNormal, sideSign);
                var dirNext = Scale(nextNormal, sideSign);
                float anglePrev = MathF.Atan2(dirPrev.Y, dirPrev.X);
                float angleNext = MathF.Atan2(dirNext.Y, dirNext.X);

                // shortest arc between the two directions
                float sweep = angleNext - anglePrev;
                if (sweep >  MathF.PI) sweep -= 2f * MathF.PI;
                if (sweep < -MathF.PI) sweep += 2f * MathF.PI;

                int segments = Math.Max(2,
                    (int)MathF.Ceiling(MathF.Abs(sweep) / (MathF.PI / 8f)));

                PointF lastArc = outerPrev;
                float lastV = cornerArcLength;
                for (int s = 1; s <= segments; s++)
                {
                    float angle = anglePrev + sweep * (s / (float)segments);
                    PointF arcPoint = s == segments
                        ? outerNext
                        : Add(corner, new PointF(
                              MathF.Cos(angle) * hw, MathF.Sin(angle) * hw));
                    // Monotone eps ladder across the fan: every triangle
                    // gets two DISTINCT arc-vertex UVs, otherwise the
                    // rasterizer drops it (degenerate position->UV map).
                    float arcV = cornerArcLength + s * UvEps;
                    AddDistanceTri(corner, 0f, lastArc, 1f, arcPoint, 1f, cornerArcLength, lastV, arcV, pos, uv);
                    lastArc = arcPoint;
                    lastV = arcV;
                }
                break;
            }
        }
    }

    /// <summary>
    /// Triangle with PER-VERTEX u values (u = distance to the stroke
    /// centerline / maxHalfWidth) and ONE shared v value (arc length of
    /// the owning point, e.g. a join corner or a round cap). The shader
    /// interpolates u across the face and produces a correct feather
    /// (antialiasing) at the outer edge.
    /// </summary>
    private static void AddTri(PointF p0, PointF p1, PointF p2,
        List<SKPoint> pos, List<SKPoint> uv,
        float u0, float u1, float u2, float v)
    {
        AddVertex(p0, u0, v, pos, uv);
        AddVertex(p1, u1, v, pos, uv);
        AddVertex(p2, u2, v, pos, uv);
    }

    /// <summary>
    /// Triangle with per-vertex distance values AND per-vertex arc
    /// lengths (v), e.g. a dash transition running through a square cap.
    /// </summary>
    private static void AddDistanceTri(
        PointF a, float uA, PointF b, float uB, PointF c, float uC,
        float vA, float vB, float vC,
        List<SKPoint> pos, List<SKPoint> uv)
    {
        AddVertex(a, uA, vA, pos, uv);
        AddVertex(b, uB, vB, pos, uv);
        AddVertex(c, uC, vC, pos, uv);
    }

    private static void AddVertex(PointF p, float u, float v,
        List<SKPoint> pos, List<SKPoint> uv)
    {
        pos.Add(new SKPoint(p.X, p.Y));
        // v = cumulative arc length along the polyline (>= 0 everywhere);
        // the shader evaluates the dash pattern from it.
        uv.Add(new SKPoint(u, v));
    }

    // --------------------------------------------------------------------
    //  Square cap: rectangle of length hw appended to the line end.
    //  Emitted as DISTANCE triangles: u = box metric max(|lateral|,
    //  along) / maxHalfWidth, which is LINEAR on each of the four
    //  triangles below (the diagonals baseCenter->capLeft/capRight are
    //  the |x| == y creases of the max metric), so the shader's regular
    //  dist = |u| * maxHalfWidth path renders the cap with correct AA
    //  -- no special shader branch needed.
    //  v = arc length + along: the dash pattern therefore cuts the cap
    //  perpendicular to the line, exactly like the line body. If the
    //  line end falls into a dash GAP the cap disappears entirely --
    //  correct Skia-like behavior (the cap belongs to the last dash).
    // --------------------------------------------------------------------
    private static void AddCapSquare(
        PointF center, PointF direction, float hw, float arcLength,
        List<SKPoint> pos, List<SKPoint> uv)
    {
        PointF outwardDir = Normalize(direction);          // pointing OUTWARD
        PointF ortho = new PointF(-outwardDir.Y, outwardDir.X);
        float vTip = arcLength + hw;                       // cap far edge

        // Five anchor points (u = box distance / maxHalfWidth):
        PointF baseCenter = center;                                 // u = 0
        PointF baseLeft   = Sub(center, Scale(ortho, hw));          // u = -1
        PointF baseRight  = Add(center, Scale(ortho, hw));          // u = +1
        PointF capLeft    = Add(baseLeft,  Scale(outwardDir, hw));  // u = -1
        PointF capRight   = Add(baseRight, Scale(outwardDir, hw));  // u = +1
        PointF capMid     = Add(center,   Scale(outwardDir, hw));   // u = +-1

        // v = arc length + along-cap fraction: EXACT at every anchor point
        // (along is a linear function of position, so v interpolates
        // exactly across each triangle and the dash cut runs
        // PERPENDICULAR to the line -- capLeft/capRight sit on the TIP
        // edge (along = hw -> v = vTip), NOT at the base!
        // Left half: metric = |lateral| (below the |x| == y crease)
        AddDistanceTri(baseCenter, 0f, baseLeft, -1f, capLeft, -1f, arcLength, arcLength, vTip, pos, uv);
        AddDistanceTri(baseCenter, 0f, capLeft, -1f, capMid, -1f, arcLength, vTip, vTip, pos, uv);
        // Right half: metric = |lateral| as well (mirrored crease)
        AddDistanceTri(baseCenter, 0f, capMid, 1f, capRight, 1f, arcLength, vTip, vTip, pos, uv);
        AddDistanceTri(baseCenter, 0f, capRight, 1f, baseRight, 1f, arcLength, vTip, arcLength, pos, uv);
    }

    /// <summary>
    /// ADAPTIVE fan resolution for round caps and round joins, derived
    /// from the tessellation RESERVE (maxHalfWidth): a cap of radius 1 px
    /// does not need 8 arc segments -- the chord error is far below one
    /// pixel -- while a cap of radius 40 px does. The step count is chosen
    /// so the visible polygon error stays sub-pixel at every tier.
    /// ALWAYS EVEN: with an odd step count one fan triangle pairs the two
    /// arc vertices at symmetric angles (sin equal) and would collapse
    /// to a DEGENERATE UV mapping -- the rasterizer drops such triangles
    /// silently (see the round-cap render test).
    /// </summary>
    private static int RoundSteps(float hw) => hw switch
    {
        < 2f  => 2,    // tiny radii: 90 degrees per step
        < 6f  => 4,    // small: 45 degrees
        < 20f => 8,    // medium: 22.5 degrees
        _     => 16,   // large radii: 11.25 degrees
    };

    // --------------------------------------------------------------------
    //  Round cap: half-disc fan, u = distance / maxHalfWidth.
    //  The shader cuts out the current width with AA.
    //  v = arcLength + ALONG-CAP displacement (the outward component of
    //  the arc point, 0 at the base edge ... hw at the tip) -- the SAME
    //  contract as the square cap, so the dash pattern runs through the
    //  cap exactly like it does for square caps.
    //  CRITICAL: v MUST vary along the arc. Skia's DrawVertices path maps
    //  position -> UV per triangle to sample the shader; a triangle whose
    //  outer vertices carry IDENTICAL UVs (u = 1, v = const on the whole
    //  arc) has a degenerate, non-invertible UV mapping and is silently
    //  DROPPED by the rasterizer -- the entire cap renders as nothing
    //  while every geometry test stays green. Do not "simplify" v back to
    //  a constant; render-test RoundCap_RendersHalfDisc_NothingBeyondRadius
    //  guards this.
    //  The fan sweeps from +side over +outward (the OUTER half-disc) to
    //  -side: rotation is side*cos + outward*sin.
    // --------------------------------------------------------------------
    private static void AddCapRound(
        PointF endPoint, PointF outwardDirection, float hw, float arcLength,
        List<SKPoint> pos, List<SKPoint> uv)
    {
        PointF outward = Scale(Normalize(outwardDirection), hw);
        PointF side = new PointF(-outward.Y, outward.X);   // one outer edge

        int steps = RoundSteps(hw);   // adaptive: see RoundSteps below
        PointF lastPt = Add(endPoint, side);
        float lastV = arcLength;                           // angle 0: base edge
        for (int i = 1; i <= steps; i++)
        {
            float angle = (i / (float)steps) * MathF.PI;   // 0..180 degrees
            float cos = MathF.Cos(angle);
            float sin = MathF.Sin(angle);

            // Rotate side counter-clockwise, passing through outward:
            PointF nextDir = new PointF(
                side.X * cos + outward.X * sin,
                side.Y * cos + outward.Y * sin);
            PointF nextPt = Add(endPoint, nextDir);
            // Outward component of the arc point = hw * sin(angle): the
            // along-cap position. Monotone within every (i-1, i) pair for
            // the even step counts used here, so no UV triangle is flat.
            float nextV = arcLength + hw * sin;

            AddDistanceTri(endPoint, 0f, lastPt, 1f, nextPt, 1f,
                           arcLength, lastV, nextV, pos, uv);
            lastPt = nextPt;
            lastV = nextV;
        }
    }

    /// <summary>
    /// Points whose distance from the SEGMENT (predecessor -> successor) is
    /// within CollinearEpsilon are dropped together with their join: a
    /// collinear middle point produces a DEGENERATE join (two zero-area
    /// triangles) that only costs tessellation time and upload bandwidth.
    /// The while-loop cascades: any run of (near-)collinear interior
    /// points collapses to its two outer points.
    /// </summary>
    private const float CollinearEpsilon = 0.01f;

    /// <summary>
    /// Distance of p from the SEGMENT a-b (clamped projection). The
    /// collinearity test MUST use the segment, not the unlimited support
    /// line: a reversal like (0,0)->(100,0)->(5,0) has its middle point
    /// ON the line through the outer points -- but outside the segment.
    /// Dropping it would flatten the spike and destroy real geometry.
    /// </summary>
    private static float DistanceToSegment(PointF p, PointF a, PointF b)
    {
        float dx = b.X - a.X, dy = b.Y - a.Y;
        float len2 = dx * dx + dy * dy;
        if (len2 < 1e-12f) return float.MaxValue;  // no usable segment
        float t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / len2,
            0f, 1f);
        float px = a.X + t * dx - p.X, py = a.Y + t * dy - p.Y;
        return MathF.Sqrt(px * px + py * py);
    }

    private static int CalcNumOfCoords(Coordinate[] coords, IReadOnlyList<Coordinate[]>? holes)
    {
        var result = 0;

        if (coords is null || coords.Length == 0)
            return result;
        
        var length = coords.Length - (coords[0].X == coords[^1].X && coords[0].Y == coords[^1].Y ? 1 : 0);

        if (length > 2)
            result = length;

        if (holes is null)
            return result;
        
        foreach (var hole in holes)
        {
            length = hole.Length - (hole[0].X == hole[^1].X && hole[^1].Y == hole[^1].Y ? 1 : 0);
            if (length > 2)
                result += length;
        }

        return result;
    }
}

// ============================================================================
// 1b) Polygon triangulation with holes (adapter around an earcut port)
// ============================================================================
/// <summary>
/// Triangulates polygons (with holes) into triangle vertex indices.
/// The triangulation itself is delegated to an earcut port; this adapter
/// exists because the different "Earcut" NuGet packages have INCOMPATIBLE
/// APIs -- adapt the two method bodies below to the package you use.
///
/// Known packages and their call signatures:
///   * "Earcut.NET" (oberbichler/earcut.net):
///       Earcut.Tessellate(IList&lt;double&gt; vertices, IList&lt;int&gt; holeIndices)
///       -> int[] triangle indices
///   * "Earcut" (MadWorldNL/EarCut):
///       Earcut.Tessellate(double[] vertices, int[] holes) -> List&lt;int&gt;
///   * "EarcutDotNet" (MichaCo/Earcut, .NET 8+):
///       Earcut.Triangulate(double[] coords) and
///       Earcut.Triangulate(double[] coords, int[] holeIndices) -> int[]
///
/// All of them are ports of Mapbox' battle-tested earcut.js. Handling holes
/// correctly requires bridging each hole into the outer ring before ear
/// clipping -- implementing that by hand is notoriously error-prone (bridge
/// selection, collinear edges, degenerate holes), so we rely on a proven
/// earcut implementation instead of writing our own.
///
/// Contract of this adapter:
///   * coords: flat [x0, y0, x1, y1, ...], outer ring first, then holes
///   * ringStarts: vertex index of each ring's first vertex (ringStarts[0]
///     is the outline; ringStarts[1..] are the holes)
///   * returns: flat triangle list [i0, i1, i2, i3, i4, i5, ...] of vertex
///     indices into the coords list
/// </summary>
public static class PolygonTriangulator
{
    /// <summary>Triangulates a polygon without holes.</summary>
    public static int[] Triangulate(ReadOnlySpan<double> coords)
    {
        // Earcut implementations work on doubles.
        //var doubles = new double[coords.Count];
        //for (int i = 0; i < coords.Count; i++) doubles[i] = coords[i];

        // ADAPT to your earcut package, e.g. (oberbichler/earcut.net):
        //   return Earcut.Tessellate(doubles);
        // or (MichaCo/Earcut):
        //   return Earcut.Triangulate(doubles);
        //return Earcut.Triangulate(doubles);
        return Earcut.Triangulate(coords);
    }

    /// <summary>Triangulates a polygon with holes.</summary>
    public static int[] TriangulateWithHoles(
        ReadOnlySpan<double> coords, IReadOnlyList<int> ringStarts)
    {
        //var doubles = new double[coords.Count];
        //for (int i = 0; i < coords.Count; i++) doubles[i] = coords[i];

        // ringStarts[0] belongs to the outline; only the holes are passed
        // to earcut as hole indices.
        var holeIndices = new int[ringStarts.Count - 1];
        for (int i = 1; i < ringStarts.Count; i++) holeIndices[i - 1] = ringStarts[i];

        // ADAPT to your earcut package, e.g. (oberbichler/earcut.net):
        //   return Earcut.Tessellate(doubles, holeIndices);
        // or (MichaCo/Earcut):
        //   return Earcut.Triangulate(doubles, holeIndices);
        //return Earcut.Triangulate(doubles, holeIndices);
        return Earcut.Triangulate(coords, holeIndices);
    }
}

//  ----------------------------------------------------------------------------
//  1) Joins: selectable via AddLines(...). All three variants (Miter, Bevel,
//     Round) use distance-based u values -> antialiasing on all outer
//     edges. Miter checks miterRatio = 2/|n1+n2| against miterLimit and
//     falls back to Bevel for very sharp angles (no spikes at ~180 deg).
//
//  2) Caps: Butt (no geometry), Square (box metric pre-baked in u, four
//     distance triangles -- no special shader branch), Round (half-disc
//     fan, u = distance, shader cuts with AA). Selectable separately for
//     start and end. All caps carry the arc length of their line end
//     (square caps + hw along the tip), so the DASH PATTERN cuts them
//     correctly: a cap only renders when its line end lies inside a dash.
//
//  3) Blend mode: always composite with SrcOver. DstOver inverts the
//     compositing order and buries joins/caps behind earlier geometry.
//
//  4) Zoom: for screen-space-constant width pass halfWidth in pixels and
//     optionally use a local matrix on the shader (WithLocalMatrix).
//
//  5) Polygon fills: use ordinary triangulation (e.g. ear clipping)
//     without the UV trick; the color also flows through a uniform
//     (a second, trivial SkSL shader with only uColor). For STROKED
//     closed polygons use GpuLineMesh.BuildClosed instead -- it appends
//     the first point once and runs the join pipeline over the seam,
//     so every corner (including start == end) gets a proper join.
//
//  6) Performance: the vertex arrays are copied to the GPU once per draw
//     call (a single memcpy via CreateCopy), no re-tessellation. That is
//     orders of magnitude cheaper than rebuilding the display list
//     including strokes every frame.
//
//  7) DASHED LINES: the vertex v coordinate carries the cumulative arc
//     length along each polyline (in px). The shader evaluates the dash
//     pattern from it with three uniforms (uDashOn/uDashOff/uDashPhase)
//     and a 1px antialiased transition -> the pattern can change at
//     runtime with ZERO re-tessellation. Details:
//       * The pattern is shifted by +0.5 px: a line starting at a dash
//         boundary renders fully opaque from the first pixel (otherwise
//         the start cap would show 50% alpha).
//       * The arc length restarts at 0 for each polyline in the mesh
//         (same behavior as Skia's per-path dash phase).
//       * Dash ends are BUTT-style cuts; per-dash caps (round dots per
//         dash) are not possible in a single static mesh. Workaround for
//         a "dotted" look: short dashOn + Round line caps gives round
//         dash endings wherever a dash ends exactly at a line end.
//       * Round caps & all joins share the arc length of their owning
//         point -> a dash transition never cuts through a join; a round
//         cap is either fully on (line end inside a dash) or fully off.
//       * Square caps extend the arc length by hw along the tip -> the
//         dash pattern cuts them perpendicular to the line, exactly like
//         the line body. An end cap in a dash gap disappears entirely.
//       * FILLS: polygon fills share this shader with v = 0 -> they sit
//         0.5 px inside the first dash -> coverage 1 -> solid, even when
//         drawn with dashOn > 0. No special handling needed.
// ============================================================================