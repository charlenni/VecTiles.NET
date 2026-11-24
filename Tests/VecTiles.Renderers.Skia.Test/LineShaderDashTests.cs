using System.Drawing;
using SkiaSharp;
using VecTiles.Renderers.Skia.Primitives.Vertices;

namespace VecTiles.Renderers.Skia.Test;

/// <summary>
/// Verifies the dash pattern of the line shader: dash and gap lengths are
/// absolute tile-space px (the converters scale their style units themselves,
/// e.g. mapsforge dash arrays do not scale with the stroke width). A pattern
/// of 10 px dash / 10 px gap with a stroke width of 6 px must produce black
/// runs of ~10 px separated by ~10 px gaps, starting with a dash at the line
/// start.
/// </summary>
public class LineShaderDashTests
{
    /// <summary>
    /// Rasterises a horizontal line with a 10/10 dash pattern and checks the
    /// black run lengths along the stroke.
    /// </summary>
    [Fact]
    public void DashLengthsAreAbsolutePixels()
    {
        var (pos, uv) = Tessellate(new PointF(8f, 32f), new PointF(248f, 32f));

        using var shader = LineShader.Create(8f);
        // stroke width 6 u px, dash pattern 10 px dash + 10 px gap
        using var update = shader.Update(6f, SKColors.Black, [10f, 10f]);
        using var paint = new SKPaint { Shader = update, IsAntialias = true };

        using var surface = SKSurface.Create(new SKImageInfo(256, 64));
        surface.Canvas.Clear(SKColors.White);
        surface.Canvas.DrawVertices(pos.Length > 0
            ? SKVertices.CreateCopy(SKVertexMode.Triangles, [.. pos], [.. uv], null)
            : throw new InvalidOperationException("no vertices"), SKBlendMode.SrcOver, paint);
        surface.Canvas.Flush();

        using var snap = surface.Snapshot();
        using var bmp = SKBitmap.FromImage(snap);

        // Scan the row through the center of the stroke
        var black = new bool[256];
        for (var x = 0; x < 256; x++)
        {
            var c = bmp.GetPixel(x, 32);
            black[x] = c.Red < 128 && c.Green < 128 && c.Blue < 128;
        }

        var runs = new List<(int Start, int Length, bool Black)>();
        for (var x = 8; x < 248; x++)
        {
            if (runs.Count > 0 && runs[^1].Black == black[x])
            {
                runs[^1] = (runs[^1].Start, runs[^1].Length + 1, runs[^1].Black);
            }
            else
            {
                runs.Add((x, 1, black[x]));
            }
        }

        // The pattern starts with a dash at the line start; alternating
        // 10/10 with half-pixel AA tolerance
        var blackRuns = runs.Where(r => r.Black).Select(r => r.Length).ToList();
        var whiteRuns = runs.Where(r => !r.Black).Select(r => r.Length).ToList();

        Assert.True(runs.All(r => r.Length is > 7 and < 13),
            "unexpected run lengths: " + string.Join(", ", runs.Select(r => $"{(r.Black ? "black" : "white")}={r.Length}")));
        Assert.True(!runs.All(r => r.Black), "line is not dashed");
        Assert.True(runs[0].Black, "pattern does not start with a dash");
        Assert.InRange(runs[0].Length, 9, 12); // first run is a ~10 px dash

        // The pattern repeats every 20 px: verify the accumulated period over the full line
        var totalDash = blackRuns.Sum();
        var totalGap = whiteRuns.Sum();
        var ratio = (double)totalDash / (totalDash + totalGap);
        Assert.Equal(0.5, ratio, 1); // half of the pattern is dash, half is gap
    }

    /// <summary>
    /// Same as above but with a stroke width 3: the dash LENGTHS must be the
    /// same (independent of the stroke width).
    /// </summary>
    [Fact]
    public void DashLengthsAreIndependentOfStrokeWidth()
    {
        var (pos, uv) = Tessellate(new PointF(8f, 32f), new PointF(248f, 32f));

        using var shader = LineShader.Create(8f);
        using var update = shader.Update(3f, SKColors.Black, [10f, 10f]);
        using var paint = new SKPaint { Shader = update, IsAntialias = true };

        using var surface = SKSurface.Create(new SKImageInfo(256, 64));
        surface.Canvas.Clear(SKColors.White);
        surface.Canvas.DrawVertices(SKVertices.CreateCopy(SKVertexMode.Triangles, [.. pos], [.. uv], null),
            SKBlendMode.SrcOver, paint);
        surface.Canvas.Flush();

        using var snap = surface.Snapshot();
        using var bmp = SKBitmap.FromImage(snap);

        var black = new List<int>();
        for (var x = 8; x < 248; x++)
        {
            var c = bmp.GetPixel(x, 32);
            if (c.Red < 128 && c.Green < 128 && c.Blue < 128) black.Add(x);
        }

        // ~half of the 240 px line length is dash coverage (10/10 pattern ±AA)
        Assert.InRange(black.Count, 110, 128);
    }

    /// <summary>
    /// Tessellates a 1 px-resolution horizontal line segment.
    /// </summary>
    private static (SKPoint[] pos, SKPoint[] uv) Tessellate(PointF from, PointF to)
    {
        var tessPos = new List<SKPoint>();
        var tessUv = new List<SKPoint>();
        Tessellation.TessellatePolyline(
            [from, to], hw: 4f,
            VecTiles.Common.Enums.StrokeCap.Butt, VecTiles.Common.Enums.StrokeCap.Butt,
            VecTiles.Common.Enums.StrokeJoin.Round, 4f, tessPos, tessUv);
        return ([.. tessPos], [.. tessUv]);
    }
}
