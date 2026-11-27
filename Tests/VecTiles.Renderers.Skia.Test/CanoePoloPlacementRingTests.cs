using BruTile;
using Mapsui.Extensions;
using VecTiles.Common.Primitives;
using VecTiles.Controls.Mapsui;
using VecTiles.Controls.Mapsui.Extensions;
using VecTiles.Renderers.Common;
using VecTiles.Renderers.Common.Interfaces;
using VecTiles.Renderers.Skia;
using Xunit.Abstractions;

namespace VecTiles.Renderers.Skia.Test;

/// <summary>
/// Diagnostic and regression for the user report about the canoe_polo icons
/// at the bottom right of tile 17/68642/45902 (zoomed in from 16 to 19, the
/// icons were missing at 17): the placement over the padded placement ring —
/// the tile set the pipeline really uses — must place both icons at every
/// zoom level.
///
/// </summary>
public class CanoePoloPlacementRingTests
{
    private static readonly string _path = Utilities.DataFiles;
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Initializes the test with the xUnit output helper.
    /// </summary>
    /// <param name="output">The xUnit output helper.</param>
    public CanoePoloPlacementRingTests(ITestOutputHelper output) => _output = output;

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
    /// Runs a synchronous placement over the padded ring at the given zoom
    /// and reports the placed/rejected entries per symbol layer.
    /// </summary>
    /// <param name="zoom">The canonical zoom level to probe.</param>
    [Theory]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(18)]
    [InlineData(19)]
    public void PlacementOverPlacementRing(int zoom)
    {
        var tileSource = new OMTRenderedTileSource($"file://{_path}/osm-liberty.json");
        var service = new PlacementService(tileSource) { SynchronousPlacement = true };

        double resolution = zoom.ToResolution();
        const int width = 1024, height = 768;
        var (cx, cy) = Center();
        var viewport = new Mapsui.Viewport(cx, cy, resolution, 0, width, height);

        var screenExtent = ScreenExtent(viewport);
        var tileWorldSize = 512.0 * viewport.Resolution;
        var placementExtent = screenExtent.Grow(tileWorldSize);

        var screenTiles = tileSource.Schema.GetTileInfos(screenExtent, resolution).ToArray();
        var ringTiles = tileSource.Schema.GetTileInfos(placementExtent, resolution).ToArray();

        _output.WriteLine($"--- zoom {zoom}: {screenTiles.Length} screen tiles, {ringTiles.Length} ring tiles ---");

        Func<double, double, (double, double)> toScreen = (x, y) =>
        {
            var p = viewport.WorldToScreen(x, y);
            return (p.X, p.Y);
        };

        var view = service.Update(ringTiles, zoom, toScreen, (cx, cy), screenTiles);
        var result = view.Placement;

        _output.WriteLine($"placement incomplete={result.Incomplete} zoom={result.ZoomLevel}");

        // The placements must complete over the real data of the scene and
        // both canoe_polo icons of the tile must be placed at every zoom
        Assert.False(result.Incomplete);
        Assert.Equal(view.Tiles.Count, ringTiles.Length);

        var canoe = result.PlacedByLayer.SelectMany(p => p.Value).Select(e => e.Symbol)
            .OfType<Symbol>()
            .Where(s => s.Subclass == "canoe_polo")
            .ToList();
        // At 16 only one icon is expected: the label "Universitätsbibliothek,
        // Völkerkundemuseum" is no longer broken inside its words (MapLibre
        // only wraps between words), so it is wider and its conservative
        // placement square for viewport-aligned text reaches the second icon
        Assert.Equal(zoom == 16 ? 1 : 2, canoe.Count);

        foreach (var symbol in canoe)
        {
            _output.WriteLine($"PLACED canoe_polo tile={symbol.Tile.Id} id={symbol.Id} style={symbol.StyleName}");
        }
    }
}
