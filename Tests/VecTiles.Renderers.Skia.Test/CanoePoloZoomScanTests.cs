using BruTile;
using Mapsui.Extensions;
using NetTopologySuite.Geometries;
using SkiaSharp;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Controls.Mapsui;
using VecTiles.Controls.Mapsui.Extensions;
using VecTiles.Renderers.Skia;
using Xunit.Abstractions;

namespace VecTiles.Renderers.Skia.Test;

/// <summary>
/// Diagnostic: the canoe_polo icons at the bottom right of tile
/// 17/68642/45902 — which of them are placed per canonical zoom level
/// (16..19), and which symbol blocks the rejected ones.
/// </summary>
public class CanoePoloZoomScanTests
{
    private static readonly string _path = Utilities.DataFiles;
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Initializes the test with the xUnit output helper.
    /// </summary>
    /// <param name="output">The xUnit output helper.</param>
    public CanoePoloZoomScanTests(ITestOutputHelper output) => _output = output;

    private const double WorldWidth = 40075016.686;
    private const double WorldHalf = WorldWidth / 2;

    private static (double X, double Y) Center()
    {
        var tileSpan17 = WorldWidth / Math.Pow(2, 17);
        return (-WorldHalf + (68642 + 464.0 / 512) * tileSpan17,
            WorldHalf - (45902 + 492.0 / 512) * tileSpan17);
    }

    private static BruTile.Extent RendererExtent(Mapsui.Viewport viewport, ITileSchema schema)
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
    /// Runs the placement at each zoom level and reports the canoe_polo
    /// symbols: placed (with envelope) or rejected (with the symbols that
    /// occupy their space).
    /// </summary>
    /// <param name="zoom">The canonical zoom level to scan.</param>
    [Theory]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(18)]
    [InlineData(19)]
    public void ScanCanoePoloPerZoom(int zoom)
    {
        var tileSource = new MLRenderedTileSource($"file://{_path}/osm-liberty.json");
        var service = new PlacementService(tileSource) { SynchronousPlacement = true };

        double resolution = zoom.ToResolution();
        const int width = 1024, height = 768;
        var (cx, cy) = Center();
        var viewport = new Mapsui.Viewport(cx, cy, resolution, 0, width, height);

        var tileInfos = tileSource.Schema.GetTileInfos(RendererExtent(viewport, tileSource.Schema), resolution)
            .ToArray();
        _output.WriteLine($"--- zoom {zoom}: {tileInfos.Length} tiles ---");

        Func<double, double, (double, double)> toScreen = (x, y) =>
        {
            var p = viewport.WorldToScreen(x, y);
            return (p.X, p.Y);
        };

        var result = service.Update(tileInfos, zoom, toScreen).Placement;

        var placedPoints = result.PlacedByLayer
            .SelectMany(p => p.Value.Select(e => e.Symbol))
            .OfType<PointSymbol>()
            .Where(s => s.Subclass == "canoe_polo")
            .ToList();
        var placedIds = new HashSet<PointSymbol>(placedPoints);

        foreach (var symbol in placedPoints)
        {
            _output.WriteLine($"PLACED tile={symbol.Tile} id={symbol.Id} style={symbol.StyleName} " +
                $"name='{symbol.Name}' rank={symbol.Rank} point={symbol.Point.X:F1},{symbol.Point.Y:F1} " +
                $"env={symbol.Envelope}");
        }

        foreach (var symbol in result.Rejected.OfType<PointSymbol>()
            .Where(s => s.Subclass == "canoe_polo"))
        {
            _output.WriteLine($"REJECTED tile={symbol.Tile} id={symbol.Id} style={symbol.StyleName} " +
                $"name='{symbol.Name}' rank={symbol.Rank} point={symbol.Point.X:F1},{symbol.Point.Y:F1} " +
                $"env={symbol.Envelope}");

            // Who occupies the space? Query the collision tree around the
            // rejection envelope
            if (symbol.Envelope is { } env)
            {
                var blockers = result.Tree.Query(env)
                    .OfType<Symbol>()
                    .Where(s => s.Envelope != null && s.Envelope.Intersects(env))
                    .ToList();

                foreach (var blocker in blockers)
                {
                    _output.WriteLine($"   BLOCKED BY tile={blocker.Tile} id={blocker.Id} " +
                        $"style={blocker.StyleName} class='{blocker.Class}' subclass='{blocker.Subclass}' " +
                        $"name='{blocker.Name}' rank={blocker.Rank} env={blocker.Envelope}");
                }

                if (blockers.Count == 0)
                {
                    _output.WriteLine("   (no overlapping box in the tree)");
                }
            }

            // Also report the screen envelope (the space actually checked)
            _output.WriteLine($"   screenEnv={symbol.ScreenEnvelope}");
        }

        if (placedPoints.Count == 0 && !result.Rejected.OfType<PointSymbol>()
                .Any(s => s.Subclass == "canoe_polo"))
        {
            _output.WriteLine("no canoe_polo symbols in the placement at all");
        }

        // The user report: the icons show at 16, are missing at 17 and come
        // back at 18/19. The placement itself must place the icons at every
        // zoom level of the scan. At 16 only one icon is expected: the label
        // "Universitätsbibliothek, Völkerkundemuseum" is no longer broken
        // inside its words (MapLibre only wraps between words), so it is
        // wider and its conservative placement square for viewport-aligned
        // text reaches the second icon
        Assert.Equal(zoom == 16 ? 1 : 2, placedPoints.Count);
    }
}
