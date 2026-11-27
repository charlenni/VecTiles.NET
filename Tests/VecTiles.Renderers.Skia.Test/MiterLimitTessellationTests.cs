using System.Drawing;
using SkiaSharp;
using VecTiles.Common.Enums;
using VecTiles.Renderers.Skia.Primitives.Vertices;

namespace VecTiles.Renderers.Skia.Test;

/// <summary>
/// Tests of the line tessellation at sharp corners: the miter limit falls
/// back to Bevel when the miter ratio (miter length / half stroke width)
/// exceeds the limit, and the corner trim must never emit a sliver beyond
/// the stroke width. This reproduces the "road spike across the lake"
/// defect seen in tile 10/536/358.
/// </summary>
public class MiterLimitTessellationTests
{
    private const float HalfWidth = 4f;

    // V-shaped polyline with a 150-degree turn: miter ratio = 1/cos(75) ~ 3.86
    private static readonly PointF[] SharpCorner =
    [
        new(0f, 0f),
        new(100f, 0f),
        new(100f + 100f * MathF.Cos(150f * MathF.PI / 180f),
            100f * MathF.Sin(150f * MathF.PI / 180f)),
    ];

    // Hairpin with a 170-degree turn and a SHORT second segment (20 px): the
    // inner offset lines of the corner intersect ~45 px behind the corner
    // (halfWidth * tan(85)), far beyond the short segment. Reproduces the
    // "road spike" defect: the trim emitted a long sliver beyond the geometry.
    private static readonly PointF[] ShortHairpin =
    [
        new(0f, 0f),
        new(100f, 0f),
        new(100f + 20f * MathF.Cos(170f * MathF.PI / 180f),
            20f * MathF.Sin(170f * MathF.PI / 180f)),
    ];

    // Zigzag of SHORT segments (16 px) with 170-degree turns: at every corner
    // the inner offset lines intersect ~45 px away (halfWidth * tan(85)) --
    // farther than BOTH adjacent segments. Reproduces the "road spike"
    // defect: the trim emitted a long sliver beyond the geometry.
    private static readonly PointF[] ShortZigzag =
    [
        new(84f, 0f),
        new(100f, 0f),
        new(100f + 16f * MathF.Cos(170f * MathF.PI / 180f),
            16f * MathF.Sin(170f * MathF.PI / 180f)),
        new(100f + 16f * MathF.Cos(170f * MathF.PI / 180f)
              + 16f * MathF.Cos(340f * MathF.PI / 180f),
            16f * MathF.Sin(170f * MathF.PI / 180f)
              + 16f * MathF.Sin(340f * MathF.PI / 180f)),
    ];

    private static List<SKPoint> Tessellate(PointF[] pts, StrokeJoin join, float miterLimit)
    {
        var pos = new List<SKPoint>();
        var uv = new List<SKPoint>();
        Tessellation.TessellatePolyline(pts, HalfWidth,
            StrokeCap.Butt, StrokeCap.Butt, join, miterLimit, pos, uv);
        return pos;
    }

    private static (float Max, SKPoint Vertex) MaxDistanceToPolyline(List<SKPoint> vertices, PointF[] pts)
    {
        var segments = new (SKPoint A, SKPoint B, float LengthSquared)[pts.Length - 1];
        for (int i = 0; i < pts.Length - 1; i++)
        {
            var a = new SKPoint(pts[i].X, pts[i].Y);
            var b = new SKPoint(pts[i + 1].X, pts[i + 1].Y);
            var ab = b - a;
            segments[i] = (a, b, ab.LengthSquared);
        }

        float max = 0f;
        var far = new SKPoint();
        foreach (var v in vertices)
        {
            // Distance to the polyline = distance to the NEAREST segment
            float min = float.MaxValue;
            foreach (var (a, b, lengthSq) in segments)
            {
                var ab = b - a;
                var av = v - a;
                float t = lengthSq < 1e-9f
                    ? 0f
                    : Math.Clamp((av.X * ab.X + av.Y * ab.Y) / lengthSq, 0f, 1f);
                min = MathF.Min(min,
                    (v - new SKPoint(a.X + t * ab.X, a.Y + t * ab.Y)).Length);
            }
            if (min > max) { max = min; far = v; }
        }
        return (max, far);
    }

    [Fact]
    public void MiterLimitExceededFallsBackToBevelTest()
    {
        // miter ratio ~3.86 > limit 2: the join must not emit a miter tip
        var pos = Tessellate(SharpCorner, StrokeJoin.Miter, 2f);

        // Bevel join emits ONE join triangle, the miter join TWO
        var miterPos = Tessellate(SharpCorner, StrokeJoin.Miter, 4f);
        Assert.Equal(miterPos.Count / 3 - 1, pos.Count / 3);

        // No vertex extends beyond the stroke half width
        var (max, far) = MaxDistanceToPolyline(pos, SharpCorner);
        Assert.True(max <= HalfWidth * 1.001f,
            $"Bevel fallback produced a spike of {max} (half width {HalfWidth}) at ({far.X}, {far.Y})");
    }

    [Fact]
    public void MiterLimitBelowRatioKeepsMiterTipTest()
    {
        // miter ratio ~3.86 <= limit 4: the miter tip exists and reaches
        // halfWidth * miterRatio from the centerline
        var pos = Tessellate(SharpCorner, StrokeJoin.Miter, 4f);

        Assert.True(MaxDistanceToPolyline(pos, SharpCorner).Max > 3f * HalfWidth,
            $"Expected a miter tip beyond 3 * half width, got {MaxDistanceToPolyline(pos, SharpCorner).Max}");
    }

    [Theory]
    [InlineData(StrokeJoin.Miter)]
    [InlineData(StrokeJoin.Round)]
    [InlineData(StrokeJoin.Bevel)]
    public void ShortHairpinTrimEmitsNoSliverTest(StrokeJoin join)
    {
        var pos = Tessellate(ShortHairpin, join, 2f);

        var (max, far) = MaxDistanceToPolyline(pos, ShortHairpin);
        Assert.True(max <= HalfWidth * 1.001f,
            $"{join} join produced a sliver of {max} (half width {HalfWidth}) at ({far.X}, {far.Y})");
    }

    [Theory]
    [InlineData(StrokeJoin.Miter)]
    [InlineData(StrokeJoin.Round)]
    [InlineData(StrokeJoin.Bevel)]
    public void ShortZigzagTrimEmitsNoSliverTest(StrokeJoin join)
    {
        var pos = Tessellate(ShortZigzag, join, 2f);

        var (max, far) = MaxDistanceToPolyline(pos, ShortZigzag);
        Assert.True(max <= HalfWidth * 1.001f,
            $"{join} join produced a sliver of {max} (half width {HalfWidth}) at ({far.X}, {far.Y})");
    }
}
