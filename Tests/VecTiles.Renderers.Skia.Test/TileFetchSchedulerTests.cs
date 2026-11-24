using BruTile;
using BruTile.Predefined;
using VecTiles.Renderers.Common;
using VecTiles.Renderers.Common.Interfaces;
using VecTiles.Controls.Mapsui;

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

        public Func<int, int, IRenderedTile?>? Respond { get; set; }

        public Task<IRenderedTile?> GetTileAsync(TileInfo tileInfo)
        {
            var (x, y, zoom) = (tileInfo.Index.Col, tileInfo.Index.Row, tileInfo.Index.Level);
            Requests.Add((x, y, zoom));
            return Task.FromResult(Respond?.Invoke(x, y));
        }

        public ITileSchema Schema { get; } = new GlobalSphericalMercator512(YAxis.OSM, 0, 24);

        public string Name => "fake";

        public Attribution Attribution => new("Fake", string.Empty);

        public float[] Center => [];

        public float? Zoom => null;
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

    private static IRenderedTile TileOf(TileInfo info)
    {
        return new RenderedTile(info.ToTile());
    }

    /// <summary>
    /// Verifies that a tile whose source answers without data is skipped and the
    /// fetch result is marked incomplete.
    /// </summary>
    [Fact]
    public async Task MissingTile_IsSkippedAndMarkedIncomplete()
    {
        var (x0, y0) = (3, 2);
        var source = new FakeRenderedTileSource
        {
            Respond = (x, y) => (x, y) == (x0, y0) ? null : TileOf(TileInfo(x, y, 8)),
        };
        var scheduler = new TileFetchScheduler(source);

        var result = await scheduler.FetchAsync(
            [TileInfo(3, 2, 8), TileInfo(4, 2, 8), TileInfo(5, 2, 8)]);

        Assert.False(result.Tiles is null);
        Assert.Equal(2, result.Tiles!.Count);
        Assert.True(result.Incomplete);
    }

    /// <summary>
    /// Verifies that a fetch failing with an exception does not fail the pass:
    /// the other tiles arrive and the result is marked incomplete.
    /// </summary>
    [Fact]
    public async Task FailingTile_DoesNotFailTheOtherTiles()
    {
        var (x1, y1) = (4, 2);
        var source = new FakeRenderedTileSource
        {
            Respond = (x, y) => (x, y) == (x1, y1)
                ? throw new InvalidOperationException("tile load failed")
                : TileOf(TileInfo(x, y, 8)),
        };
        var scheduler = new TileFetchScheduler(source);

        var result = await scheduler.FetchAsync(
            [TileInfo(3, 2, 8), TileInfo(4, 2, 8), TileInfo(5, 2, 8)]);

        Assert.Equal(2, result.Tiles!.Count);
        Assert.True(result.Incomplete);
    }

    /// <summary>
    /// Verifies that with a single worker the tile nearest to the given world
    /// center is fetched first.
    /// </summary>
    [Fact]
    public async Task CenterFirst_SingleWorkerFetchesNearestTileFirst()
    {
        var source = new FakeRenderedTileSource
        {
            Respond = (x, y) => TileOf(TileInfo(x, y, 8)),
        };
        var scheduler = new TileFetchScheduler(source, maxConcurrentFetches: 1);

        await scheduler.FetchAsync(
            [TileInfo(3, 2, 8), TileInfo(4, 2, 8), TileInfo(5, 2, 8), TileInfo(6, 2, 8)],
            centerWorld: (2.5 * 512, -2.5 * 512));

        // The tile whose center is nearest to the given world center must be
        // fetched first
        Assert.Equal((3, 2, 8), source.Requests[0]);
    }

    /// <summary>
    /// Verifies that a fetch pass with an already cancelled token throws
    /// <see cref="OperationCanceledException" /> before any further fetch starts.
    /// </summary>
    [Fact]
    public async Task CancelledFetch_ThrowsImmediately()
    {
        // The source call itself carries no token (IRenderedTileSource.GetTileAsync),
        // so a running fetch cannot be aborted; a cancelled token must at
        // least stop the pass before any further fetch starts
        var source = new FakeRenderedTileSource
        {
            Respond = (x, y) => TileOf(TileInfo(x, y, 8)),
        };
        var scheduler = new TileFetchScheduler(source, maxConcurrentFetches: 1);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => scheduler.FetchAsync(
            [TileInfo(3, 2, 8), TileInfo(4, 2, 8), TileInfo(5, 2, 8), TileInfo(6, 2, 8)],
            token: cts.Token));
    }
}
