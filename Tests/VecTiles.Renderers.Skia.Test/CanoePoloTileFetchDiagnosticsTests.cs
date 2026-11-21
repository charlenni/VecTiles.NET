using BruTile;
using Mapsui.Extensions;
using VecTiles.Controls.Mapsui;
using VecTiles.Controls.Mapsui.Extensions;
using VecTiles.Renderers.Common.Interfaces;
using Xunit.Abstractions;

namespace VecTiles.Renderers.Skia.Test;

/// <summary>
/// Diagnostic and regression: every tile of the padded placement ring of the
/// canoe_polo view (tile 17/68642/45902) must be fetchable per zoom level
/// (the tile source coverage of the local MBTiles file has no holes here).
/// </summary>
public class CanoePoloTileFetchDiagnosticsTests
{
    private static readonly string _path = Utilities.DataFiles;
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Initializes the test with the xUnit output helper.
    /// </summary>
    /// <param name="output">The xUnit output helper.</param>
    public CanoePoloTileFetchDiagnosticsTests(ITestOutputHelper output) => _output = output;

    private const double WorldWidth = 40075016.686;
    private const double WorldHalf = WorldWidth / 2;

    private static (double X, double Y) Center()
    {
        var tileSpan17 = WorldWidth / Math.Pow(2, 17);
        return (-WorldHalf + (68642 + 464.0 / 512) * tileSpan17,
            WorldHalf - (45902 + 492.0 / 512) * tileSpan17);
    }

    private static BruTile.Extent ScreenExtent(Mapsui.Viewport viewport)
    {
        var point1 = viewport.ScreenToWorld(0, 0);
        var point2 = viewport.ScreenToWorld(viewport.Width, 0);
        var point3 = viewport.ScreenToWorld(0, viewport.Height);
        var point4 = viewport.ScreenToWorld(viewport.Width, viewport.Height);

        var minX = Math.Min(Math.Min(point1.X, point2.X), Math.Min(point3.X, point4.X));
        var minY = Math.Min(Math.Min(point1.Y, point2.Y), Math.Min(point3.Y, point4.Y));
        var maxX = Math.Max(Math.Max(point1.X, point2.X), Math.Max(point3.X, point4.X));
        var maxY = Math.Max(Math.Max(point1.Y, point2.Y), Math.Max(point3.Y, point4.Y));

        return new BruTile.Extent(minX, minY, maxX, maxY);
    }

    /// <summary>
    /// Fetches every tile of the padded placement ring at each zoom level and
    /// reports the tiles that the source refuses.
    /// </summary>
    /// <param name="zoom">The canonical zoom level to probe.</param>
    [Theory]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(18)]
    [InlineData(19)]
    public async Task ProbePlacementRingFetches(int zoom)
    {
        var tileSource = new OMTRenderedTileSource($"file://{_path}/osm-liberty.json");

        double resolution = zoom.ToResolution();
        const int width = 1024, height = 768;
        var (cx, cy) = Center();
        var viewport = new Mapsui.Viewport(cx, cy, resolution, 0, width, height);

        var screenExtent = ScreenExtent(viewport);
        var tileWorldSize = 512.0 * viewport.Resolution;
        var placementExtent = screenExtent.Grow(tileWorldSize);

        var screenTiles = tileSource.Schema.GetTileInfos(screenExtent, resolution).ToArray();
        var allTiles = tileSource.Schema.GetTileInfos(placementExtent, resolution).ToArray();

        _output.WriteLine($"--- zoom {zoom}: {screenTiles.Length} screen tiles, {allTiles.Length} placement ring tiles ---");

        foreach (var info in allTiles)
        {
            var isScreen = screenTiles.Any(s => s.Index.Equals(info.Index));

            IRenderedTile? result = null;
            try
            {
                result = await tileSource.GetTileAsync(info);
            }
            catch (Exception ex)
            {
                _output.WriteLine($"THREW  tile=({info.Index.Col},{info.Index.Row})@{info.Index.Level} screen={isScreen}: {ex.GetType().Name}");
            }

            _output.WriteLine($"{(result == null ? "NULL   " : "OK     ")} tile=({info.Index.Col},{info.Index.Row})@{info.Index.Level} screen={isScreen}");

            // Every tile of the padded placement ring of this scene must be
            // fetchable: a hole in the coverage outside the screen must not
            // stop the placement (see BlocksPublication), and there must be
            // no hole at all here
            Assert.NotNull(result);
        }
    }
}
