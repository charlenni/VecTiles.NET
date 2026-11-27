using Mapsui.Extensions;
using SkiaSharp;
using VecTiles.Controls.Mapsui;
using VecTiles.Controls.Mapsui.Extensions;

namespace VecTiles.Renderers.Skia.Test;

/// <summary>
/// Diagnostic for the app-only report (canoe_polo icons missing at zoom 17):
/// the tests elsewhere run with density 1 — here the real layer pipeline
/// draws with a density-scaled canvas (like a HiDPI screen) and probes the
/// icon pixels in device pixels.
/// </summary>
public class CanoePoloHiDpiTests
{
    private static readonly string _path = Utilities.DataFiles;
    private readonly Xunit.Abstractions.ITestOutputHelper _output;

    public CanoePoloHiDpiTests(Xunit.Abstractions.ITestOutputHelper output) => _output = output;

    private const double WorldWidth = 40075016.686;
    private const double WorldHalf = WorldWidth / 2;

    private static (double X, double Y) Center()
    {
        var tileSpan17 = WorldWidth / Math.Pow(2, 17);
        return (-WorldHalf + (68642 + 464.0 / 512) * tileSpan17,
            WorldHalf - (45902 + 492.0 / 512) * tileSpan17);
    }

    [Theory]
    [InlineData(17.0, 1.5)]
    [InlineData(17.0, 2.0)]
    [InlineData(17.6, 2.0)]
    public async Task DensityCanvas_ShowsIcons(double zoom, double density)
    {
        var tileSource = new MLRenderedTileSource($"file://{_path}/osm-liberty.json");
        using var layer = new RenderedTileLayer(tileSource) { PlacementDebounceMs = 0 };

        var (cx, cy) = Center();
        var resolution = zoom.ToResolution();
        var viewport = new Mapsui.Viewport(cx, cy, resolution, 0, 1024, 768);

        var (w, h) = ((int)(1024 * density), (int)(768 * density));
        using var bitmap = new SKBitmap(w, h);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        canvas.Scale((float)density);

        // let the placement settle at this zoom (idle frames like the app).
        // The pipeline is asynchronous (tile fetch and placement run on the
        // thread pool): under load one frame may lag behind the placement
        // that just completed, so poll until the handed-out result is
        // complete and no holdover instead of a fixed frame budget
        var settled = false;
        for (var frame = 0; frame < 300 && !settled; frame++)
        {
            await Task.Delay(150);

            RenderedTileLayerRenderer.Draw(canvas, viewport, layer, new Mapsui.Rendering.RenderService());

            var diag = layer.CurrentPlacement?.Placement;
            settled = diag is { Incomplete: false, IsHoldover: false };
        }

        Assert.True(settled, $"placement of zoom {zoom} did not settle");

        // One more idle frame with the settled placement, so the icon of the
        // final result is on the canvas even when the last draw raced the
        // placement completion
        await Task.Delay(150);
        RenderedTileLayerRenderer.Draw(canvas, viewport, layer, new Mapsui.Rendering.RenderService());
        canvas.Flush();

        // icon screen positions (logical px) at this zoom
        var span17 = WorldWidth / Math.Pow(2, 17);
        var positions = new (double Px, double Py)[]
        {
            (949949.2, 6002757.5),
            (949912.2, 6002771.8),
        };

        var deviceX = 512 * density;
        var deviceY = 384 * density;

        foreach (var (px, py) in positions)
        {
            var sx = deviceX + (px - cx) / (span17 / 512 / Math.Pow(2, zoom - 17)) * density;
            var sy = deviceY - (py - cy) / (span17 / 512 / Math.Pow(2, zoom - 17)) * density;
            var ix = (int)Math.Round(sx);
            var iy = (int)Math.Round(sy);

            var distinctColors = new HashSet<string>();
            for (var dy = -8 * density; dy <= 8 * density; dy += density)
            {
                for (var dx = -8 * density; dx <= 8 * density; dx += density)
                {
                    distinctColors.Add(bitmap.GetPixel((int)(ix + dx), (int)(iy + dy)).ToString());
                }
            }

            _output.WriteLine($"zoom={zoom} density={density} icon at device ({ix},{iy}): " +
                $"{bitmap.GetPixel(ix, iy)}, {distinctColors.Count} distinct colors");

            Assert.True(distinctColors.Count > 1,
                $"density {density}, zoom {zoom}: icon position ({ix},{iy}) uniform background");
        }
    }
}
