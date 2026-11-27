using BruTile;
using Mapsui.Extensions;
using SkiaSharp;
using VecTiles.Controls.Mapsui;
using VecTiles.Controls.Mapsui.Extensions;
using Xunit.Abstractions;

namespace VecTiles.Renderers.Skia.Test;

/// <summary>
/// Temporary diagnostic: the zoom-out path. Stable draw at 17, then a draw
/// at 16.9 canonical zoom 16 while the placement of 16 is still pending
/// (async): does the holdover keep the symbols on the screen?
/// </summary>
public class CanoePoloZoomOutTests
{
    private static readonly string _path = Utilities.DataFiles;
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Initializes the test with the xUnit output helper.
    /// </summary>
    /// <param name="output">The xUnit output helper.</param>
    public CanoePoloZoomOutTests(ITestOutputHelper output) => _output = output;

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
        var path = Path.Combine(Utilities.TestFiles, "Results", $"canoe-out-{name}.png");
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.OpenWrite(path);
        data.SaveTo(stream);
    }

    /// <summary>
    /// Draws the canoe_polo scene at stable zoom 17 and then at 16.9 (canonical zoom 16)
    /// while the placement run of 16 is still pending, to document the holdover behavior
    /// when zooming out.
    /// </summary>
    [Fact]
    public async Task ZoomOutFrom17()
    {
        var tileSource = new MLRenderedTileSource($"file://{_path}/osm-liberty.json");
        using var layer = new RenderedTileLayer(tileSource);

        DrawAt(layer, 17.0, "stable17");
        await Task.Delay(4000); // let the async placement of 17 finish

        DrawAt(layer, 17.0, "stable17b");

        // Cross the zoom border downwards: canonical 16, no placement yet
        DrawAt(layer, 16.9, "holdover-before-16-run");
        await Task.Delay(120);
        DrawAt(layer, 16.9, "holdover-mid");

        // Settle: the run of 16 completes and replaces the holdover (fade)
        await Task.Delay(1200);
        DrawAt(layer, 16.9, "after-16-run");
        await Task.Delay(1200);
        DrawAt(layer, 16.9, "after-16-run2");

        _output.WriteLine("done");
    }
}
