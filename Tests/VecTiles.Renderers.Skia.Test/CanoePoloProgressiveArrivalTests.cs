using Mapsui.Extensions;
using VecTiles.Common.Primitives;
using VecTiles.Controls.Mapsui;
using VecTiles.Controls.Mapsui.Extensions;
using VecTiles.Renderers.Common;
using VecTiles.Renderers.Common.Interfaces;
using VecTiles.Renderers.Skia;

namespace VecTiles.Renderers.Skia.Test;

/// <summary>
/// Regression for the user report about the canoe_polo icons (tile
/// 17/68642/45902, missing at zoom 17): the placement runs repeatedly while
/// the tiles of the placement ring arrive one by one, as in the app. Every
/// partial pass produced a line-label box that — with the old grow-only
/// collision tree — kept blocking the icons forever, and a rejected symbol
/// was pinned as rejected, so it was never even examined again. With the
/// per-pass rebuilt tree and the unpinned rejections, the full ring of the
/// settled view must place both icons.
/// </summary>
public class CanoePoloProgressiveArrivalTests
{
    private static readonly string _path = Utilities.DataFiles;

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

    [Fact]
    public void ProgressiveArrival_BothIconsPlacedInTheSettledView()
    {
        var tileSource = new MLRenderedTileSource($"file://{_path}/osm-liberty.json");
        var service = new PlacementService(tileSource) { SynchronousPlacement = true };

        const int zoom = 17;
        double resolution = zoom.ToResolution();
        const int width = 1024, height = 768;
        var (cx, cy) = Center();
        var viewport = new Mapsui.Viewport(cx, cy, resolution, 0, width, height);

        var screenExtent = ScreenExtent(viewport);
        var tileWorldSize = 512.0 * viewport.Resolution;
        var placementExtent = screenExtent.Grow(tileWorldSize);

        var screenTiles = tileSource.Schema.GetTileInfos(screenExtent, resolution).ToArray();
        var ringTiles = tileSource.Schema.GetTileInfos(placementExtent, resolution).ToArray();

        Func<double, double, (double, double)> toScreen = (x, y) =>
        {
            var p = viewport.WorldToScreen(x, y);
            return (p.X, p.Y);
        };

        int CanoeCount(PlacementResult result) => result.PlacedByLayer
            .SelectMany(p => p.Value).Select(e => e.Symbol)
            .OfType<Symbol>().Count(s => s.Subclass == "canoe_polo");

        // As in the app: every arrived tile triggers a placement pass over
        // the tiles seen so far (the ring arrives around the loaded view)
        PlacementResult last = PlacementResult.Empty();
        for (var i = 0; i < ringTiles.Length; i++)
        {
            last = service.Update(ringTiles.Take(i + 1).ToList(), zoom, toScreen, (cx, cy), screenTiles)
                .Placement;
        }

        Assert.Equal(2, CanoeCount(last));
        Assert.False(last.Incomplete);
    }
}
