using BruTile;
using BruTile.Predefined;
using VecTiles.Renderers.Skia;
using static VecTiles.Renderers.Skia.Test.PlacementServiceTestSupport;

namespace VecTiles.Renderers.Skia.Test;

/// <summary>
/// Regression tests for the publication rule of a placement pass whose tile
/// set is incomplete: a missing tile of the padding ring (a tile source with
/// a limited coverage, e.g. a local MBTiles file) must not block the
/// publication of the placement — everything on screen is placed from
/// complete data. A missing screen tile keeps the result incomplete (the
/// holdover of the previous zoom stays on screen, its symbols may still
/// change when the tile arrives).
/// </summary>
public class PlacementScreenTilePublicationTests
{
    /// <summary>
    /// The ring tile set of the test: four tiles in a row, the two middle
    /// ones are the screen tiles, the outer ones the padding ring.
    /// </summary>
    private static readonly (int Col, int Row, int Zoom)[] _ring =
        [(3, 2, 8), (4, 2, 8), (5, 2, 8), (6, 2, 8)];

    private static TileInfo[] Ring() => _ring.Select(t => TileInfo(t.Col, t.Row, t.Zoom)).ToArray();

    private static TileInfo[] Screen() => [TileInfo(4, 2, 8), TileInfo(5, 2, 8)];

    [Fact]
    public void MissingPaddingTile_PublishesThePlacement()
    {
        var source = new FakeRenderedTileSource { MissingTiles = { 6 } };
        var service = new PlacementService(source) { SynchronousPlacement = true };

        var view = service.Update(Ring(), 8, (x, y) => (x, y), null, Screen());

        Assert.False(view.Placement.Incomplete);
        Assert.Equal(3, view.Tiles.Count);
    }

    [Fact]
    public void MissingScreenTile_KeepsTheResultIncomplete()
    {
        var source = new FakeRenderedTileSource { MissingTiles = { 4 } };
        var service = new PlacementService(source) { SynchronousPlacement = true };

        var view = service.Update(Ring(), 8, (x, y) => (x, y), null, Screen());

        Assert.True(view.Placement.Incomplete);
        Assert.Equal(3, view.Tiles.Count);
    }

    [Fact]
    public void WithoutScreenTileSet_EveryMissingTileBlocks()
    {
        var source = new FakeRenderedTileSource { MissingTiles = { 6 } };
        var service = new PlacementService(source) { SynchronousPlacement = true };

        var view = service.Update(Ring(), 8, (x, y) => (x, y));

        Assert.True(view.Placement.Incomplete);
    }
}
