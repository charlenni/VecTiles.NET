using System.Drawing;
using SkiaSharp;
using VecTiles.Common.Enums;
using VecTiles.Renderers.Skia.Primitives.Vertices;

namespace VecTiles.Renderers.Skia.Test;

/// <summary>
/// Tests of the line-round-limit conversion in the line tessellation: a
/// ROUND join at a SHALLOW angle (small deflection from straight) must be
/// converted to the (visually identical, cheaper) miter join when the miter
/// ratio stays within the round limit, and must stay a round fan otherwise.
///
/// The discriminator is the outermost vertex extent along the outer angle
/// bisector near the corner: a round fan never leaves the arc (projection
/// &lt;= halfWidth), a miter tip reaches halfWidth * miterRatio. The corner
/// trim points sit on the INNER bisector, so they do not interfere.
/// </summary>
public class RoundLimitTessellationTests
{
    private const float HalfWidth = 4f;

    // Shallow corner with a 30-degree deflection:
    // miter ratio = 1 / cos(15) ~ 1.035 -- below the spec default 1.05.
    private static readonly PointF[] ShallowCorner =
    [
        new(0f, 0f),
        new(100f, 0f),
        new(100f + 100f * MathF.Cos(30f * MathF.PI / 180f),
            100f * MathF.Sin(30f * MathF.PI / 180f)),
    ];

    // Sharp corner with a 170-degree turn:
    // miter ratio = 1 / cos(85) ~ 11.5 -- far above any round limit.
    private static readonly PointF[] SharpCorner =
    [
        new(0f, 0f),
        new(100f, 0f),
        new(100f + 100f * MathF.Cos(170f * MathF.PI / 180f),
            100f * MathF.Sin(170f * MathF.PI / 180f)),
    ];

    private static List<SKPoint> Tessellate(PointF[] pts, float roundLimit)
    {
        var pos = new List<SKPoint>();
        var uv = new List<SKPoint>();
        Tessellation.TessellatePolyline(pts, HalfWidth,
            StrokeCap.Butt, StrokeCap.Butt, StrokeJoin.Round, miterLimit: 4f,
            pos, uv, roundLimit: roundLimit);
        return pos;
    }

    /// <summary>
    /// Largest projection of the vertices near the corner onto the outer
    /// angle bisector: halfWidth for a round fan, halfWidth * miterRatio
    /// for a miter tip.
    /// </summary>
    private static float MaxOuterBisectorExtent(List<SKPoint> vertices, PointF[] pts)
    {
        var corner = new SKPoint(pts[1].X, pts[1].Y);
        var d1 = new SKPoint(pts[1].X - pts[0].X, pts[1].Y - pts[0].Y);
        var d2 = new SKPoint(pts[2].X - pts[1].X, pts[2].Y - pts[1].Y);
        var n1 = new SKPoint(-d1.Y, d1.X);
        var n2 = new SKPoint(-d2.Y, d2.X);
        float l1 = MathF.Sqrt(n1.X * n1.X + n1.Y * n1.Y);
        float l2 = MathF.Sqrt(n2.X * n2.X + n2.Y * n2.Y);
        n1 = new SKPoint(n1.X / l1, n1.Y / l1);
        n2 = new SKPoint(n2.X / l2, n2.Y / l2);

        float cross = n1.X * n2.Y - n1.Y * n2.X;
        float sideSign = cross > 0f ? -1f : 1f;     // outer side of the corner
        var bisector = new SKPoint(n1.X + n2.X, n1.Y + n2.Y);
        float bisLen = MathF.Sqrt(bisector.X * bisector.X + bisector.Y * bisector.Y);
        bisector = new SKPoint(sideSign * bisector.X / bisLen,
                               sideSign * bisector.Y / bisLen);

        // Only vertices of the join (within 2 * halfWidth of the corner);
        // the segment quads extend far along the polyline.
        float radius = 2f * HalfWidth;
        float max = float.NegativeInfinity;
        foreach (var v in vertices)
        {
            var rel = v - corner;
            if (rel.Length > radius) continue;
            max = MathF.Max(max, rel.X * bisector.X + rel.Y * bisector.Y);
        }
        return max;
    }

    [Fact]
    public void ShallowRoundJoinConvertsToMiterTest()
    {
        // Ratio ~1.035 <= limit 1.05: the join must emit the miter tip at
        // halfWidth * miterRatio ~ 4.14 instead of staying on the arc.
        var pos = Tessellate(ShallowCorner, 1.05f);

        var extent = MaxOuterBisectorExtent(pos, ShallowCorner);
        Assert.True(extent > HalfWidth * 1.02f,
            $"Expected a miter tip beyond {HalfWidth * 1.02f}, got {extent}");
    }

    [Fact]
    public void ShallowRoundJoinStaysRoundBelowLimitTest()
    {
        // Ratio ~1.035 > limit 1.02: no conversion, the join stays on the
        // round arc (projection <= halfWidth).
        var pos = Tessellate(ShallowCorner, 1.02f);

        var extent = MaxOuterBisectorExtent(pos, ShallowCorner);
        Assert.True(extent <= HalfWidth * 1.001f,
            $"Expected a round fan within {HalfWidth * 1.001f}, got {extent}");
    }

    [Fact]
    public void SharpRoundJoinNeverConvertsTest()
    {
        // Ratio ~11.5 > limit 1.05: no conversion even at the spec default.
        var pos = Tessellate(SharpCorner, 1.05f);

        var extent = MaxOuterBisectorExtent(pos, SharpCorner);
        Assert.True(extent <= HalfWidth * 1.001f,
            $"Expected a round fan within {HalfWidth * 1.001f}, got {extent}");
    }
}
