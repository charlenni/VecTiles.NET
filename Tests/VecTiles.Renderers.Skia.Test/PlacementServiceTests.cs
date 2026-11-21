using BruTile;
using BruTile.Predefined;
using VecTiles.Renderers.Common;
using VecTiles.Renderers.Common.Interfaces;
using VecTiles.Controls.Mapsui;
using VecTiles.Renderers.Skia;

namespace VecTiles.Renderers.Skia.Test;

/// <summary>
/// Tests for the placement machinery: the retained set (latch) while a new
/// pass runs, the reset of the latch on a zoom change and the incomplete
/// retry cooldown.
/// </summary>
public class PlacementServiceTests
{
    /// <summary>
    /// A fake rendered tile source whose fetches block on a gate, so the test
    /// decides when a placement pass completes.
    /// </summary>
    private sealed class GatedRenderedTileSource : IRenderedTileSource
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
    private sealed class FakeRenderedTileSource : IRenderedTileSource
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

    private static TileInfo TileInfo(int x, int y, int zoom)
    {
        return new TileInfo
        {
            Index = new TileIndex(x, y, zoom),
            Extent = new Extent(x * 512, -(y + 1) * 512, (x + 1) * 512, -y * 512),
        };
    }

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
    public void ZoomChange_ClearsTheRetainedSet()
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

        // Zoom change: the retained set belongs to the old zoom; until the
        // placement of the new zoom exists, the empty result must be handed
        // out, not the symbols of the old zoom
        var view = service.Update(keySameTilesHigherZoom, 9, (x, y) => (x, y));
        Assert.Empty(view.Placement.PlacedByLayer);
        Assert.NotSame(resultZoom8, view.Placement);

        source.Gate.TrySetResult(true);
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
