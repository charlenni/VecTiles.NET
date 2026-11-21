using BruTile;
using BruTile.Predefined;
using Mapsui;
using VecTiles.Controls.Mapsui;
using VecTiles.Controls.Mapsui.Extensions;
using VecTiles.Renderers.Common;

namespace VecTiles.Renderers.Skia.Test;

/// <summary>
/// Tests for the fetch scheduler of the placement pass: bounded parallel
/// fetching, center-first priority and the missing-tile handling.
/// </summary>
public class TileFetchSchedulerTests
{
    /// <summary>
    /// A fake rendered tile source that records the fetch order and answers
    /// via the <see cref="Respond"/> callback.
    /// </summary>
    private sealed class FakeRenderedTileSource : IRenderedTileSource
    {
        public List<(int X, int Y, int Zoom)> Requests { get; } = [];

        public Func<int, int, IFeature?>? Respond { get; set; }

        public Task<IFeature?> GetTileAsync(TileInfo tileInfo)
        {
            var (x, y, zoom) = (tileInfo.Index.Col, tileInfo.Index.Row, tileInfo.Index.Level);
            Requests.Add((x, y, zoom));
            return Task.FromResult(Respond?.Invoke(x, y));
        }

        public ITileSchema Schema { get; } = new GlobalSphericalMercator512(YAxis.OSM, 0, 24);

        public string Name => "fake";

        public Attribution Attribution => new("Fake", string.Empty);
    }

    private static TileInfo TileInfo(int x, int y, int zoom)
    {
        // The extents are only used for the priority order; plausible values
        // of a 512 tile are not required by the scheduler
        var size = 512;
        return new TileInfo
        {
            Index = new TileIndex(x, y, zoom),
            Extent = new Extent(x * size, -(y + 1) * size, (x + 1) * size, -y * size),
        };
    }

    private static RenderedTileFeature FeatureOf(TileInfo info)
    {
        var renderedTile = new RenderedTile(info.ToTile());
        return new RenderedTileFeature(renderedTile, info.Extent.MinX, info.Extent.MinY, info.Extent.MaxX, info.Extent.MaxY);
    }

    [Fact]
    public async Task MissingTile_IsSkippedAndMarkedIncomplete()
    {
        var (x0, y0) = (3, 2);
        var source = new FakeRenderedTileSource
        {
            Respond = (x, y) => (x, y) == (x0, y0) ? null : FeatureOf(TileInfo(x, y, 8)),
        };
        var scheduler = new TileFetchScheduler(source);

        var result = await scheduler.FetchAsync(
            [TileInfo(3, 2, 8), TileInfo(4, 2, 8), TileInfo(5, 2, 8)]);

        Assert.False(result.Features is null);
        Assert.Equal(2, result.Features!.Count);
        Assert.True(result.Incomplete);
    }

    [Fact]
    public async Task FailingTile_DoesNotFailTheOtherTiles()
    {
        var (x1, y1) = (4, 2);
        var source = new FakeRenderedTileSource
        {
            Respond = (x, y) => (x, y) == (x1, y1)
                ? throw new InvalidOperationException("tile load failed")
                : FeatureOf(TileInfo(x, y, 8)),
        };
        var scheduler = new TileFetchScheduler(source);

        var result = await scheduler.FetchAsync(
            [TileInfo(3, 2, 8), TileInfo(4, 2, 8), TileInfo(5, 2, 8)]);

        Assert.Equal(2, result.Features!.Count);
        Assert.True(result.Incomplete);
    }

    [Fact]
    public async Task CenterFirst_SingleWorkerFetchesNearestTileFirst()
    {
        var source = new FakeRenderedTileSource
        {
            Respond = (x, y) => FeatureOf(TileInfo(x, y, 8)),
        };
        var scheduler = new TileFetchScheduler(source, maxConcurrentFetches: 1);

        await scheduler.FetchAsync(
            [TileInfo(3, 2, 8), TileInfo(4, 2, 8), TileInfo(5, 2, 8), TileInfo(6, 2, 8)],
            centerWorld: new MPoint(2.5 * 512, -2.5 * 512));

        // The tile whose center is nearest to the given world center must be
        // fetched first
        Assert.Equal((3, 2, 8), source.Requests[0]);
    }

    [Fact]
    public async Task IncompleteResult_NoNewFetchAttemptsWithinCooldown()
    {
        var (x0, y0) = (3, 2);
        var source = new FakeRenderedTileSource
        {
            // One tile stays unavailable: the pass over the tile set never
            // completes
            Respond = (x, y) => (x, y) == (x0, y0) ? null : FeatureOf(TileInfo(x, y, 8)),
        };
        var symbolsLayer = new RenderedSymbolsLayer(source);
        var tileInfos = new[] { TileInfo(3, 2, 8), TileInfo(4, 2, 8) };

        // First call starts the background pass and returns null; the layer
        // fires DataChanged when the result is stored
        var placed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        symbolsLayer.DataChanged += (_, _) => placed.TrySetResult(true);

        Assert.Null(symbolsLayer.GetPlacementResult(tileInfos, 8, (x, y) => (x, y)));

        // Wait for the (incomplete) result without polling: under the load of
        // parallel render tests the background pass can take longer than any
        // fixed attempt loop
        placed.Task.Wait(TimeSpan.FromSeconds(30));

        Assert.NotNull(symbolsLayer.GetPlacementResult(tileInfos, 8, (x, y) => (x, y)));

        // Further frame calls within the retry cooldown must not start new
        // fetch passes: without the cooldown every call would refetch both
        // tiles (retry spin against a permanently unavailable source). No
        // delays between the calls: the loop must stay far inside the 2 s
        // cooldown even under the load of parallel tests
        for (var frames = 0; frames < 20; frames++)
        {
            Assert.NotNull(symbolsLayer.GetPlacementResult(tileInfos, 8, (x, y) => (x, y)));
        }

        Assert.Equal(2, source.Requests.Count);
    }
}
