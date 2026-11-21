using System.Drawing;
using SkiaSharp;
using VecTiles.Common.Enums;
using VecTiles.Common.Primitives;
using VecTiles.Renderers.Skia.Primitives.Vertices;

namespace VecTiles.Renderers.Skia.Test;

/// <summary>
/// Golden image tests for the LineShader pattern mode (line-pattern with a
/// sprite from the atlas). The sprite is a synthetic 8x8 PNG (vertical gradient
/// from bright red to dark red) so the pattern anchoring along AND across the
/// line is observable in the golden image.
/// </summary>
public class LineShaderPatternTests
{
    private readonly string OriginalsFolder = Path.Combine(Utilities.TestFiles, "Originals");
    private readonly string ResultsFolder = Path.Combine(Utilities.TestFiles, "Results");

    private const float MaxHalfWidth = 8f;
    private const float StrokeWidth = 8f;

    /// <summary>
    /// Encodes an 8x8 RGBA bitmap as PNG: the left half is a vertical
    /// brightness gradient (row 0 = bright red, row 7 = dark red), the right
    /// half is transparent. The a pattern therefore repeats along the line
    /// (4 px red / 4 px gap) AND shows the gradient across it.
    /// </summary>
    private static byte[] CreatePatternPng(byte r, byte g, byte b)
    {
        using var bmp = new SKBitmap(8, 8, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                if (x >= 4)
                {
                    bmp.SetPixel(x, y, SKColors.Transparent);
                    continue;
                }

                float t = y / 7f;                                       // 0 (top, bright) .. 1 (bottom, dark)
                bmp.SetPixel(x, y, new SKColor(
                    (byte)(r * (1 - 0.6f * t)),
                    (byte)(g * (1 - 0.6f * t)),
                    (byte)(b * (1 - 0.6f * t))));
            }
        }

        using var data = bmp.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static Sprite CreateGradientPatternSprite(float pixelRatio)
    {
        // 8x8 atlas px / pixelRatio => one pattern repeat per 8 world px.
        return new Sprite(new Bitmap(CreatePatternPng(255, 0, 0)), 0, 0, 8, 8, pixelRatio);
    }

    private static SKVertices CreateLineVertices(PointF[] pts)
    {
        var pos = new List<SKPoint>();
        var uv = new List<SKPoint>();
        Tessellation.TessellatePolyline(pts, MaxHalfWidth,
            StrokeCap.Butt, StrokeCap.Butt, StrokeJoin.Miter, 4f, pos, uv);
        return SKVertices.CreateCopy(SKVertexMode.Triangles, pos.ToArray(), uv.ToArray(), null);
    }

    private void DrawAndCompare(SKVertices vertices, SKShader shader, string filename)
    {
        using var bitmap = Utilities.CreateBitmap(96, 96);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Black);
            using var paint = new SKPaint { IsAntialias = true, Shader = shader };
            canvas.DrawVertices(vertices, SKBlendMode.SrcOver, paint);
        }

        if (!Directory.Exists(ResultsFolder))
        {
            Directory.CreateDirectory(ResultsFolder);
        }

        var resultPath = Path.Combine(ResultsFolder, filename);
        Utilities.SaveBitmap(bitmap, resultPath);

        var goldenPath = Path.Combine(OriginalsFolder, filename);

        if (Environment.GetEnvironmentVariable("VECTILES_RENDER_TEST_UPDATE_GOLDEN") == "true")
        {
            Utilities.SaveBitmap(bitmap, goldenPath);
            return;
        }

        Assert.True(Utilities.CompareBitmaps(resultPath, goldenPath),
            $"Rendering differs from golden image '{goldenPath}'. Actual: '{resultPath}'. " +
            "Set VECTILES_RENDER_TEST_UPDATE_GOLDEN=true to recreate the golden image.");
    }

    /// <summary>
    /// Verifies that the pattern of a horizontal line matches the golden image:
    /// gradient across the line, repeating along it.
    /// </summary>
    [Fact]
    public void Pattern_HorizontalLine_MatchesGoldenImage()
    {
        var vertices = CreateLineVertices([new PointF(8f, 48f), new PointF(88f, 48f)]);

        // Gradient top->bottom and a transparent gap every 4 px: the line's
        // upper half (across < 0 maps to sprite top) must be bright red, the
        // lower half dark red, repeating every 8 px along the line.
        using var shader = LineShader.Create(MaxHalfWidth);
        using var update = shader.Update(StrokeWidth, SKColors.Magenta, null, 0f,
            CreateGradientPatternSprite(1f));

        DrawAndCompare(vertices, update, "Line-Pattern-Horizontal.png");
    }

    /// <summary>
    /// Verifies that the pattern rotates with a diagonal line so the gradient stays
    /// perpendicular to the line direction.
    /// </summary>
    [Fact]
    public void Pattern_DiagonalLine_MatchesGoldenImage()
    {
        // The pattern must rotate with the line: along = arc length,
        // across = signed lateral distance. A diagonal line therefore shows
        // the gradient perpendicular to the diagonal.
        var vertices = CreateLineVertices([new PointF(8f, 88f), new PointF(88f, 8f)]);

        using var shader = LineShader.Create(MaxHalfWidth);
        using var update = shader.Update(StrokeWidth, SKColors.Magenta, null, 0f,
            CreateGradientPatternSprite(1f));

        DrawAndCompare(vertices, update, "Line-Pattern-Diagonal.png");
    }

    /// <summary>
    /// Verifies that a @2x pattern sprite repeats at its CSS size (one repeat
    /// per 4 world pixels) along the line.
    /// </summary>
    [Fact]
    public void Pattern_PixelRatioTwo_MatchesGoldenImage()
    {
        // A @2x sprite (8x8 atlas px, pixel ratio 2) must repeat once per
        // 4 world px (8 / 2) along the line.
        var vertices = CreateLineVertices([new PointF(8f, 48f), new PointF(88f, 48f)]);

        using var shader = LineShader.Create(MaxHalfWidth);
        using var update = shader.Update(StrokeWidth, SKColors.Magenta, null, 0f,
            CreateGradientPatternSprite(2f));

        DrawAndCompare(vertices, update, "Line-Pattern-PixelRatio2.png");
    }
}
