using BruTile;
using BruTile.Predefined;
using NetTopologySuite.Geometries;
using VecTiles.Common.Primitives;
using VecTiles.Renderers.Common;
using VecTiles.Renderers.Common.Interfaces;
using VecTiles.Controls.Mapsui;
using VecTiles.Renderers.Skia;
using static VecTiles.Renderers.Skia.Test.PlacementServiceTestSupport;

namespace VecTiles.Renderers.Skia.Test;

/// <summary>
/// Shared helpers for the placement service test classes.
/// </summary>
internal static class PlacementServiceTestSupport
{
    /// <summary>
    /// A fake rendered tile source whose fetches block on a gate, so the test
    /// decides when a placement pass completes.
    /// </summary>
    public sealed class GatedRenderedTileSource : IRenderedTileSource
    {
        public TaskCompletionSource<bool> Gate { get; set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ITileSchema Schema { get; } = new GlobalSphericalMercator512(YAxis.OSM, 0, 24);

        public string Name => "fake";

        public Attribution Attribution => new("Fake", string.Empty);

        public async Task<IRenderedTile?> GetTileAsync(TileInfo tileInfo)
        {
            await Gate.Task.ConfigureAwait(false);
            return new RenderedTile(tileInfo.ToTile());
        }
    }

    /// <summary>
    /// A fake rendered tile source that answers every request with an empty
    /// rendered tile (and one tile per key in <see cref="MissingTiles"/>
    /// with null, so a pass stays incomplete).
    /// </summary>
    public sealed class FakeRenderedTileSource : IRenderedTileSource
    {
        public List<(int X, int Y, int Zoom)> Requests { get; } = [];

        public HashSet<int> MissingTiles { get; } = [];

        public ITileSchema Schema { get; } = new GlobalSphericalMercator512(YAxis.OSM, 0, 24);

        public string Name => "fake";

        public Attribution Attribution => new("Fake", string.Empty);

        public Task<IRenderedTile?> GetTileAsync(TileInfo tileInfo)
        {
            var (x, y, zoom) = (tileInfo.Index.Col, tileInfo.Index.Row, tileInfo.Index.Level);
            Requests.Add((x, y, zoom));
            return Task.FromResult<IRenderedTile?>(MissingTiles.Contains(x) ? null : new RenderedTile(tileInfo.ToTile()));
        }
    }

    public static TileInfo TileInfo(int x, int y, int zoom)
    {
        return new TileInfo
        {
            Index = new TileIndex(x, y, zoom),
            Extent = new Extent(x * 512, -(y + 1) * 512, (x + 1) * 512, -y * 512),
        };
    }
}

/// <summary>
/// Tests for the placement machinery: the retained set (latch) while a new
/// pass runs, the rescaled holdover of the previous zoom on a zoom change
/// and the incomplete retry cooldown.
/// </summary>
public class PlacementServiceTests
{
    [Fact]
    public void RetainedSet_KeepsSymbolsWhileNewPassRuns()
    {
        var source = new GatedRenderedTileSource();
        var service = new PlacementService(source) { PlacementDebounceMs = 0 };

        var keyA = new[] { TileInfo(3, 2, 8) };
        var keyB = new[] { TileInfo(7, 2, 8) };

        // First pass starts, blocked on the gate; nothing happened before,
        // so there is nothing to retain and an empty result is returned. The
        // service fires PlacementCompleted once per finished pass; the test
        // waits for the first and the second fire separately
        var fired = 0;
        var firstPlacement = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondPlacement = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.PlacementCompleted += () =>
        {
            var current = Interlocked.Increment(ref fired);
            if (current == 1)
            {
                firstPlacement.TrySetResult(true);
            }

            if (current == 2)
            {
                secondPlacement.TrySetResult(true);
            }
        };

        Assert.Empty(service.Update(keyA, 8, (x, y) => (x, y)).Placement.PlacedByLayer);

        // Finish the first pass; the next call for the same tile set returns
        // the (cached) result
        source.Gate.TrySetResult(true);
        Assert.True(firstPlacement.Task.Wait(TimeSpan.FromSeconds(30)), "first placement pass did not finish");
        var resultA = service.Update(keyA, 8, (x, y) => (x, y)).Placement;

        // Viewport change: a new tile set (key B) has no result yet and its
        // pass runs in the background, blocked on a new gate. The previous
        // set must be handed out, not an empty result: the symbols stay on
        // the screen until the new set exists
        source.Gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Assert.Same(resultA, service.Update(keyB, 8, (x, y) => (x, y)).Placement);

        // As soon as the new pass finishes, the new result replaces the old
        source.Gate.TrySetResult(true);
        Assert.True(secondPlacement.Task.Wait(TimeSpan.FromSeconds(30)), "second placement pass did not finish");
        var resultB = service.Update(keyB, 8, (x, y) => (x, y)).Placement;
        Assert.NotSame(resultA, resultB);
    }

    [Fact]
    public void ZoomChange_HandsOutTheRetainedSetAsRescaledHoldover()
    {
        var source = new GatedRenderedTileSource
        {
            // Only the tile of the second key exists, so the first pass
            // completes and the second stays incomplete (gate still closed)
            // while the zoom changes
        };
        var service = new PlacementService(source) { PlacementDebounceMs = 0 };

        var keyA = new[] { TileInfo(3, 2, 8) };
        var keySameTilesHigherZoom = new[] { TileInfo(3, 2, 9) };

        var firstPlacementDone = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.PlacementCompleted += () => firstPlacementDone.TrySetResult(true);

        // Finish the placement of zoom 8
        Assert.Empty(service.Update(keyA, 8, (x, y) => (x, y)).Placement.PlacedByLayer);
        source.Gate.TrySetResult(true);
        Assert.True(firstPlacementDone.Task.Wait(TimeSpan.FromSeconds(30)), "first placement pass did not finish");
        var resultZoom8 = service.Update(keyA, 8, (x, y) => (x, y)).Placement;

        // Zoom change: until the placement of the new zoom exists, the
        // retained set of the old zoom is handed out as a holdover, so the
        // labels stay on the screen. It is a rescaled copy (not the same
        // instance), marked for the current zoom and incomplete
        var view = service.Update(keySameTilesHigherZoom, 9, (x, y) => (x, y));
        Assert.NotSame(resultZoom8, view.Placement);
        Assert.Equal(9, view.Placement.ZoomLevel);
        Assert.True(view.Placement.Incomplete);

        // The holdover replaces itself only when a new pass finished; an
        // identical call hands out the same stable instance again
        Assert.Same(view.Placement, service.Update(keySameTilesHigherZoom, 9, (x, y) => (x, y)).Placement);

        source.Gate.TrySetResult(true);
    }

    [Fact]
    public void Holdover_LineRunsAreRescaledIntoTheNewZoomSpace()
    {
        // Along-path distances live in the canonical placement space of the
        // zoom the placement was computed for; the holdover of a previous
        // zoom must scale them by resolution(old) / resolution(new) = 2^(old - new)
        var run = new LineSymbolPlacementRun(
        [
            new[]
            {
                new LineSymbolAnchorPlacement(100f, true, true,
                    new PathTextPlacement(false, 40f, 80f, [])),
            },
        ]);
        var symbol = new LineSymbol(new Tile(1, 1, 8), 1,
            new LineString(new[] { new Coordinate(0, 0), new Coordinate(1, 1) }), null, null);
        var result = new PlacementResult
        {
            PlacedByLayer = new Dictionary<string, List<PlacementEntry>>
            {
                ["road_label"] = [new PlacementEntry(symbol, run)],
            },
            ZoomLevel = 8,
        };

        var rescaled = result.Rescaled(9);

        var anchor = Assert.Single(Assert.Single(rescaled.PlacedByLayer["road_label"][0].Run!.Contours));
        Assert.Equal(50f, anchor.Position);
        Assert.Equal(20f, anchor.Placement!.DrawStart);
        Assert.Equal(40f, anchor.Placement.PathLength);

        // Rescaling back restores the original distances
        var back = rescaled.Rescaled(8);
        var anchorBack = Assert.Single(Assert.Single(back.PlacedByLayer["road_label"][0].Run!.Contours));
        Assert.Equal(100f, anchorBack.Position);
        Assert.Equal(40f, anchorBack.Placement!.DrawStart);
        Assert.Equal(80f, anchorBack.Placement.PathLength);
    }

    [Fact]
    public void PlacementFade_StartsWhenHoldoverIsReplaced()
    {
        var layer = new RenderedTileLayer(new FakeRenderedTileSource());

        var holdover = PlacementResult.Empty() with
        {
            ZoomLevel = 8,
            Incomplete = true,
            IsHoldover = true,
        };

        // First draw of a view: there is nothing to fade
        var (old, oldAlpha, newAlpha) = layer.GetPlacementFade(holdover);
        Assert.Null(old);
        Assert.Equal(0f, oldAlpha);
        Assert.Equal(1f, newAlpha);

        // The holdover is replaced by the finished placement: the fade runs
        // (the holdover fades out, the new set fades in)
        var finished = PlacementResult.Empty() with { ZoomLevel = 9 };
        (old, oldAlpha, newAlpha) = layer.GetPlacementFade(finished);
        Assert.Same(holdover, old);
        Assert.InRange(oldAlpha, 0f, 1f);
        Assert.InRange(newAlpha, 0f, 1f);

        // A new holdover (another zoom change) swaps instantly, no fade
        var holdover2 = holdover with { ZoomLevel = 10 };
        (old, oldAlpha, newAlpha) = layer.GetPlacementFade(holdover2);
        Assert.Null(old);
        Assert.Equal(0f, oldAlpha);
        Assert.Equal(1f, newAlpha);
    }

    [Fact]
    public void IncompleteResult_NoNewFetchAttemptsWithinCooldown()
    {
        var (x0, y0) = (3, 2);
        var source = new FakeRenderedTileSource
        {
            // One tile stays unavailable: the pass over the tile set never
            // completes
            MissingTiles = { x0 },
        };
        var service = new PlacementService(source) { SynchronousPlacement = true };
        var tileInfos = new[] { TileInfo(3, 2, 8), TileInfo(4, 2, 8) };

        // First call stores the (incomplete) result
        var first = service.Update(tileInfos, 8, (x, y) => (x, y));
        Assert.True(first.Placement.Incomplete);
        Assert.Equal(2, source.Requests.Count);

        // Further frame calls within the retry cooldown must not start new
        // fetch passes: without the cooldown every call would refetch both
        // tiles (retry spin against a permanently unavailable source). No
        // delays between the calls: the loop must stay far inside the 2 s
        // cooldown even under the load of parallel tests
        for (var frames = 0; frames < 20; frames++)
        {
            Assert.True(service.Update(tileInfos, 8, (x, y) => (x, y)).Placement.Incomplete);
        }

        Assert.Equal(2, source.Requests.Count);
    }

    [Fact]
    public void TileArrived_FiresForEachTileOfTheView()
    {
        var (x0, y0) = (3, 2);
        var source = new FakeRenderedTileSource
        {
            MissingTiles = { (x0 + 1) },
        };
        var service = new PlacementService(source) { SynchronousPlacement = true };
        var tileInfos = new[] { TileInfo(3, 2, 8), TileInfo(4, 2, 8) };

        var arrived = new List<IRenderedTile>();
        service.TileArrived += tile => { lock (arrived) arrived.Add(tile); };

        service.Update(tileInfos, 8, (x, y) => (x, y));

        Assert.Single(arrived);
    }
}

/// <summary>
/// Tests that a pan keeps the tiles of the old view that still belong to the
/// new tile set: they draw immediately, so the map does not flicker while
/// panning (no empty frames until the fetch pass delivers).
/// </summary>
public class PlacementServiceTileRetentionTests
{
    [Fact]
    public void Pan_KeepsOverlappingTilesOfThePreviousView()
    {
        var source = new FakeRenderedTileSource();
        var service = new PlacementService(source) { SynchronousPlacement = true };

        var setA = new[] { TileInfo(3, 2, 8), TileInfo(4, 2, 8) };
        service.Update(setA, 8, (x, y) => (x, y));
        Assert.Equal(2, source.Requests.Count);

        // Pan: the new tile set shifts by one tile, tile 5/2 is new and
        // tile 3/2 falls out of the view. Tile 4/2 must still be in the view
        // snapshot immediately (before any fetch of the new set arrives)
        var setB = new[] { TileInfo(4, 2, 8), TileInfo(5, 2, 8) };
        var view = service.Update(setB, 8, (x, y) => (x, y));

        var ids = view.Tiles.Select(t => (t.Info.Index.Col, t.Info.Index.Row, t.Info.Index.Level)).ToList();
        Assert.Contains((4, 2, 8), ids);
        Assert.DoesNotContain((3, 2, 8), ids);
        Assert.Equal(2, ids.Count);
    }

    [Fact]
    public void PanOfTheWholeView_TileSetUntouched_KeepsAllTiles()
    {
        var source = new FakeRenderedTileSource();
        var service = new PlacementService(source) { SynchronousPlacement = true };

        var setA = new[] { TileInfo(3, 2, 8), TileInfo(4, 2, 8) };
        service.Update(setA, 8, (x, y) => (x, y));
        var countAfterFirstUpdate = source.Requests.Count;

        // Same tile set, different key (the tiles key contains the tile ids,
        // so the key would differ only for a different set) — pass the same
        // infos again: no change at all, no new fetches
        service.Update(setA, 8, (x, y) => (x, y));
        Assert.Equal(countAfterFirstUpdate, source.Requests.Count);
        Assert.Equal(2, source.Requests.Count);
    }

    [Fact]
    public void ZoomChange_DropsAllTiles()
    {
        var source = new FakeRenderedTileSource();
        var service = new PlacementService(source) { SynchronousPlacement = true };

        var setA = new[] { TileInfo(3, 2, 8) };
        service.Update(setA, 8, (x, y) => (x, y));

        var setB = new[] { TileInfo(3, 2, 9) };
        var view = service.Update(setB, 9, (x, y) => (x, y));

        // Synchronous pass with a fake source filling the view again; the
        // retained (wrong level) tile must not appear
        Assert.All(view.Tiles, t => Assert.Equal(9, t.Info.Index.Level));
    }
}
