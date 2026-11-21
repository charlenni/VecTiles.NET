using Mapsui.Extensions;
using SkiaSharp;
using VecTiles.Controls.Mapsui;
using VecTiles.Controls.Mapsui.Extensions;
using Xunit.Abstractions;

namespace VecTiles.Renderers.Skia.Test;

/// <summary>
/// Diagnostic: zoom sequence 16 -> 17 -> 18 -> 19 through the real layer
/// pipeline (async placement) over the canoe_polo scene, capturing a frame
/// after each settled zoom. Reproduces the user report: the icons show at
/// 16, disappear at 17, return at 18/19.
/// </summary>
public class CanoePoloZoomInPipelineTests
{
    private static readonly string _path = Utilities.DataFiles;
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Initializes the test with the xUnit output helper.
    /// </summary>
    /// <param name="output">The xUnit output helper.</param>
    public CanoePoloZoomInPipelineTests(ITestOutputHelper output) => _output = output;

    private const double WorldWidth = 40075016.686;
    private const double WorldHalf = WorldWidth / 2;

    private static (double X, double Y) Center()
    {
        var tileSpan17 = WorldWidth / Math.Pow(2, 17);
        return (-WorldHalf + (68642 + 464.0 / 512) * tileSpan17,
            WorldHalf - (45902 + 492.0 / 512) * tileSpan17);
    }

    private static void DrawAt(RenderedTileLayer layer, double zoom, string name)
    {
        var (cx, cy) = Center();
        var resolution = zoom.ToResolution();
        var viewport = new Mapsui.Viewport(cx, cy, resolution, 0, 1024, 768);
        using var bitmap = new SKBitmap(1024, 768);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        RenderedTileLayerRenderer.Draw(canvas, viewport, layer, new Mapsui.Rendering.RenderService());
        canvas.Flush();
        Directory.CreateDirectory(Path.Combine(Utilities.TestFiles, "Results"));
        var path = Path.Combine(Utilities.TestFiles, "Results", $"canoe-pipeline-{name}.png");
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.OpenWrite(path);
        data.SaveTo(stream);

        // TEMP-DIAG: crops around the icon positions (64x64, scaled 4x) for
        // a visual check of the small icons
        var (ccx, ccy) = Center();
        var span17 = WorldWidth / Math.Pow(2, 17);
        var positions = new (double Px, double Py)[]
        {
            (949949.2, 6002757.5),
            (949912.2, 6002771.8),
        };
        foreach (var (px, py) in positions)
        {
            var sx = (int)Math.Round(512 + (px - ccx) / (span17 / 512 / Math.Pow(2, zoom - 17)));
            var sy = (int)Math.Round(384 - (py - ccy) / (span17 / 512 / Math.Pow(2, zoom - 17)));

            var cropRect = new SKRectI(sx - 32, sy - 32, sx + 32, sy + 32);
            var crop = new SKBitmap(64, 64);
            using (var ccanvas = new SKCanvas(crop))
            {
                ccanvas.DrawBitmap(bitmap, cropRect,
                    new SKRect(0, 0, 64, 64),
                    new SKPaint { IsAntialias = false });
            }

            using var cropImage = SKImage.FromBitmap(crop);
            using var cropData = cropImage.Encode(SKEncodedImageFormat.Png, 100);
            var cropPath = Path.Combine(Utilities.TestFiles, "Results",
                $"canoe-crop-{name}-x{sx}-y{sy}.png");
            using var cropStream = File.OpenWrite(cropPath);
            cropData.SaveTo(cropStream);
        }
    }

    /// <summary>
    /// Zooms step by step from 16 to 19 through the pipeline: settles at each
    /// canonical zoom and draws a frame, writing the result bitmaps.
    /// </summary>
    [Fact]
    public async Task ZoomInSequence()
    {
        var tileSource = new OMTRenderedTileSource($"file://{_path}/osm-liberty.json");
        using var layer = new RenderedTileLayer(tileSource);

        DrawAt(layer, 16.0, "16-a");
        await AnimatedSettle(layer, 16.0, "16");

        DrawAt(layer, 17.0, "17-first");
        await AnimatedSettle(layer, 17.0, "17");

        // Regression: after the placement has settled at 17, both canoe_polo
        // icons of the tile must be on the bitmap (the user report: missing
        // at 17, there at 16 and 18/19)
        AssertIconPixels(layer, 17.0, "17-a7");

        DrawAt(layer, 18.0, "18-first");
        await AnimatedSettle(layer, 18.0, "18");
        AssertIconPixels(layer, 18.0, "18-a7");

        DrawAt(layer, 19.0, "19-first");
        await AnimatedSettle(layer, 19.0, "19");
        AssertIconPixels(layer, 19.0, "19-a7");
    }

    /// <summary>
    /// Asserts that both icon positions of the settled frame show icon
    /// pixels (more than one distinct color) and that the hit test finds the
    /// canoe_polo features there.
    /// </summary>
    /// <param name="layer">The layer the frame was drawn from.</param>
    /// <param name="zoom">The zoom of the frame (for the screen projection).</param>
    /// <param name="name">The frame name (without the canoe-pipeline prefix).</param>
    private void AssertIconPixels(RenderedTileLayer layer, double zoom, string name)
    {
        var (cx, cy) = Center();
        var span17 = WorldWidth / Math.Pow(2, 17);
        var positions = new (double Px, double Py)[]
        {
            (949949.2, 6002757.5),
            (949912.2, 6002771.8),
        };

        var path = Path.Combine(Utilities.TestFiles, "Results", $"canoe-pipeline-{name}.png");
        using var bitmap = SKBitmap.Decode(path);

        var resolution = zoom.ToResolution();
        var viewport = new Mapsui.Viewport(cx, cy, resolution, 0, 1024, 768);

        foreach (var (px, py) in positions)
        {
            var sx = (int)Math.Round(512 + (px - cx) / (span17 / 512 / Math.Pow(2, zoom - 17)));
            var sy = (int)Math.Round(384 - (py - cy) / (span17 / 512 / Math.Pow(2, zoom - 17)));

            var distinctColors = new HashSet<string>();
            for (var dy = -8; dy <= 8; dy++)
            {
                for (var dx = -8; dx <= 8; dx++)
                {
                    distinctColors.Add(bitmap.GetPixel(sx + dx, sy + dy).ToString());
                }
            }

            Assert.True(distinctColors.Count > 1,
                $"frame {name}: icon position ({sx},{sy}) shows a uniform background " +
                $"({bitmap.GetPixel(sx, sy)}) — the icon is not drawn");

            var features = layer.GetPointFeaturesAt(sx, sy, viewport);
            Assert.Contains(features, f => f.SourceSubclass == "canoe_polo");
        }
    }

    /// <summary>
    /// Simulates the app redraw loop over the placement crossfade: draws
    /// several frames at the given zoom while the placement settles, probing
    /// the icon pixels of each frame like the ticker-driven redraws of the
    /// real map control.
    /// </summary>
    private async Task AnimatedSettle(RenderedTileLayer layer, double zoom, string name)
    {
        for (var frame = 0; frame < 8; frame++)
        {
            await Task.Delay(120);
            DrawAt(layer, zoom, $"{name}-a{frame}");
            ProbePixels2(layer, zoom, name, frame);
            ProbeIcons(layer, zoom, $"{name}-a{frame}");
        }
    }

    /// <summary>
    /// Samples the frame PNG at the projected icon positions (computed the
    /// same way as in <see cref="ProbeIcons"/>) to check whether icon pixels
    /// actually reach the bitmap.
    /// </summary>
    private void ProbePixels2(RenderedTileLayer layer, double zoom, string name, int frame)
    {
        var (cx, cy) = Center();

        var span17 = WorldWidth / Math.Pow(2, 17);
        var positions = new (double Px, double Py)[]
        {
            (949949.2, 6002757.5),
            (949912.2, 6002771.8),
        };

        var path = Path.Combine(Utilities.TestFiles, "Results", $"canoe-pipeline-{name}-a{frame}.png");
        using var bitmap = SKBitmap.Decode(path);

        foreach (var (px, py) in positions)
        {
            var sx = (int)Math.Round(512 + (px - cx) / (span17 / 512 / Math.Pow(2, zoom - 17)));
            var sy = (int)Math.Round(384 - (py - cy) / (span17 / 512 / Math.Pow(2, zoom - 17)));

            var distinctColors = new HashSet<string>();
            for (var dy = -8; dy <= 8; dy++)
            {
                for (var dx = -8; dx <= 8; dx++)
                {
                    distinctColors.Add(bitmap.GetPixel(sx + dx, sy + dy).ToString());
                }
            }

            var color = bitmap.GetPixel(sx, sy);
            _output.WriteLine($"pixel {name}-a{frame} at ({sx},{sy}): {color}, " +
                $"{distinctColors.Count} distinct colors in +-{8}px window");
        }
    }

    private void ProbeIcons(RenderedTileLayer layer, double zoom, string name)
    {
        var (cx, cy) = Center();
        var resolution = zoom.ToResolution();
        var viewport = new Mapsui.Viewport(cx, cy, resolution, 0, 1024, 768);

        var span17 = WorldWidth / Math.Pow(2, 17);
        var positions = new (double Px, double Py)[]
        {
            (949949.2, 6002757.5),
            (949912.2, 6002771.8),
        };

        foreach (var (px, py) in positions)
        {
            var sx = 512 + (px - cx) / (span17 / 512 / Math.Pow(2, zoom - 17));
            var sy = 384 - (py - cy) / (span17 / 512 / Math.Pow(2, zoom - 17));

            var features = layer.GetPointFeaturesAt(sx, sy, viewport);
            _output.WriteLine($"probe {name} at ({sx:F0},{sy:F0}): {features.Count} features " +
                $"-> {string.Join("; ", features.Take(3).Select(f => $"{f.SourceClass}/{f.SourceSubclass} '{f.Title}'"))}");
        }
    }
}
