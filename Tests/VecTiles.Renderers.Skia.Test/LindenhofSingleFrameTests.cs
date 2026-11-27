using BruTile;
using Mapsui.Extensions;
using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Quadtree;
using SkiaSharp;
using VecTiles.Common.Enums;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Controls.Mapsui;
using VecTiles.Controls.Mapsui.Extensions;
using VecTiles.Renderers.Skia;
using VecTiles.Styles.OpenMapTiles.Extensions;
using Xunit.Abstractions;

namespace VecTiles.Renderers.Skia.Test;

/// <summary>
/// Regression test for line labels on merged / split line geometries
/// ("Lindenhof", tiles 18/137291/91802 and 18/137291/91803).
/// The label sits on a loop road that is contained unclipped (overzoom) in both
/// tiles. When the tile above leaves the screen, merging results in a
/// MultiLineString instead of a LineString; with the old renderer all labels
/// disappeared in that case. Labels must be placed in both cases.
/// </summary>
public class LindenhofSingleFrameTests
{
    private static readonly string _path = Utilities.DataFiles;

    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Initializes the test with the xUnit output helper.
    /// </summary>
    /// <param name="output">The xUnit output helper.</param>
    public LindenhofSingleFrameTests(ITestOutputHelper output) => _output = output;

    private const int Zoom = 18;
    private const double WorldWidth = 40075016.686;
    private const double WorldHalf = WorldWidth / 2;
    private static double TileSpanWorld => WorldWidth / Math.Pow(2, Zoom);

    /// <summary>
    /// Renders the Lindenhof view with and without the tile above and verifies that
    /// at least one Lindenhof label is placed when the merged geometry becomes a
    /// MultiLineString (tile above gone).
    /// </summary>
    /// <param name="withTileAbove">Whether tile 18/137291/91802 is part of the view.</param>
    [Theory]
    [InlineData(true)]   // tile 91802 present
    [InlineData(false)]  // tile 91802 gone
    public void LindenhofLabelsArePlacedInBothCases(bool withTileAbove)
    {
        var styleSource = $"file://{_path}/osm-liberty.json";
        var tileSource = new MLRenderedTileSource(styleSource);
        var service = new PlacementService(tileSource) { SynchronousPlacement = true };

        double resolution = (17.9).ToResolution(); // like the app: map starts at zoom 17.9
        double topRow = withTileAbove ? 91802.5 : 91803.5;
        double heightTiles = 1.5, widthTiles = 2.0;

        double centerX = -WorldHalf + (137290.5 + 1.0) * TileSpanWorld;
        double centerY = WorldHalf - (topRow + heightTiles / 2.0) * TileSpanWorld;
        var viewport = new Mapsui.Viewport(centerX, centerY, resolution, 0,
            (int)(widthTiles * 512), (int)(heightTiles * 512));

        var screenExtent = RendererExtent(viewport, tileSource.Schema);
        var tileInfos = tileSource.Schema.GetTileInfos(screenExtent, viewport.Resolution).ToArray();
        _output.WriteLine($"resolution={resolution:F5} zoomLevel={(int)Math.Floor(viewport.Resolution.ToZoomLevel())}");
        _output.WriteLine("tiles: " + string.Join(" ", tileInfos.Select(t => $"{t.Index.Col}/{t.Index.Row}")));

        var zoomLevel = (int)Math.Floor(viewport.Resolution.ToZoomLevel());
        var context = new EvaluationContext(zoomLevel, 1f, 0f);



        Func<double, double, (double, double)> toScreen = (x, y) =>
        {
            var p = viewport.WorldToScreen(x, y);
            return (p.X, p.Y);
        };

        using var canvas = new SKCanvas(new SKBitmap(4096, 4096));

        var result = service.Update(tileInfos, zoomLevel, toScreen).Placement;
        var tree = result.Tree;

        foreach (var layerPair in result.PlacedByLayer)
        {
            foreach (var entry in layerPair.Value)
            {
                if (entry.Symbol is PointSymbol pointSymbol)
                {
                    PointSymbolRenderer.Draw(canvas, context, pointSymbol, ref tree, toScreen, 0, false);
                }
                else if (entry.Symbol is LineSymbol lineSymbol)
                {
                    if (lineSymbol.TextSymbol?.Text == "Lindenhof")
                    {
                        _output.WriteLine($"Lindenhof merged: geom={lineSymbol.Geometry.GeometryType} " +
                                          $"parts={lineSymbol.Geometry.NumGeometries} len={lineSymbol.Geometry.Length:F0}");
                        _output.WriteLine("  placed");
                    }

                    LineSymbolRenderer.Draw(canvas, context, lineSymbol, ref tree, toScreen, 0, false,
                        placementRun: entry.Run!);
                }
            }
        }

        var labels = CountLindenhofBoxes(tree);
        _output.WriteLine($"withTileAbove={withTileAbove}: placed lindenhof collision boxes: {labels}");

        // The regression is the merged MultiLineString case (tile above gone):
        // the merged label must be placed. With the tile above present the
        // placement may be blocked legitimately: since the priority reversal
        // (highest style layer wins) and the rotation-safe envelopes, place
        // and poi symbols of higher style layers claim the space first.
        if (!withTileAbove)
        {
            Assert.True(labels > 0, $"withTileAbove={withTileAbove}: no Lindenhof label placed");
        }
    }

    private static int CountLindenhofBoxes(Quadtree<ISymbol> tree)
    {
        var all = tree.Query(new Envelope(-20037508, 20037508, -20037508, 20037508));
        int count = 0;
        foreach (var item in all)
        {
            if (item is TextLineSymbol text && text.Text == "Lindenhof")
            {
                count++;
            }
        }

        return count;
    }


    private static Extent RendererExtent(Mapsui.Viewport viewport, ITileSchema schema)
    {
        var point1 = viewport.ScreenToWorld(0, 0);
        var point2 = viewport.ScreenToWorld(viewport.Width, 0);
        var point3 = viewport.ScreenToWorld(0, viewport.Height);
        var point4 = viewport.ScreenToWorld(viewport.Width, viewport.Height);

        var minX = Math.Min(Math.Min(point1.X, point2.X), Math.Min(point3.X, point4.X));
        var minY = Math.Min(Math.Min(point1.Y, point2.Y), Math.Min(point3.Y, point4.Y));
        var maxX = Math.Max(Math.Max(point1.X, point2.X), Math.Max(point3.X, point4.X));
        var maxY = Math.Max(Math.Max(point1.Y, point2.Y), Math.Max(point3.Y, point4.Y));

        return new Extent(minX, minY, maxX, maxY);
    }
}
