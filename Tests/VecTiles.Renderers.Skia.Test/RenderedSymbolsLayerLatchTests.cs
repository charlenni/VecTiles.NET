using BruTile;
using Mapsui;
using Mapsui.Layers;
using VecTiles.Controls.Mapsui;
using VecTiles.Controls.Mapsui.Extensions;
using VecTiles.Renderers.Common;

namespace VecTiles.Renderers.Skia.Test;

/// <summary>
/// Tests for the retained symbol set: while a new placement is computed (for
/// a changed tile set after panning), GetPlacementResult keeps returning the
/// last handed-out result, so the symbols stay on the screen until the new
/// set exists.
/// </summary>
public class RenderedSymbolsLayerLatchTests
{
    /// <summary>
    /// A fake rendered tile source whose passes block on a gate, so the test
    /// decides when a placement pass completes.
    /// </summary>
    private sealed class GatedRenderedTileSource : IRenderedTileSource
    {
        public TaskCompletionSource<bool> Gate { get; set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ITileSchema Schema { get; } = new GlobalSphericalMercator512(YAxis.OSM, 0, 24);

        public string Name => "fake";

        public Attribution Attribution => new("Fake", string.Empty);

        public async Task<IFeature?> GetTileAsync(TileInfo tileInfo)
        {
            await Gate.Task.ConfigureAwait(false);
            var renderedTile = new RenderedTile(tileInfo.ToTile());
            return new RenderedTileFeature(renderedTile,
                tileInfo.Extent.MinX, tileInfo.Extent.MinY, tileInfo.Extent.MaxX, tileInfo.Extent.MaxY);
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
        var layer = new RenderedSymbolsLayer(source);

        var keyA = new[] { TileInfo(3, 2, 8) };
        var keyB = new[] { TileInfo(7, 2, 8) };

        // First pass starts, blocked on the gate; nothing happened before,
        // so there is nothing to retain and null is returned. The layer
        // fires DataChanged once per finished pass; the test waits for the
        // first and the second fire separately
        var fired = 0;
        var firstPlacement = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondPlacement = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        layer.DataChanged += (_, _) =>
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

        Assert.Null(layer.GetPlacementResult(keyA, 8, (x, y) => (x, y)));

        // Finish the first pass; the next call for the same tile set returns
        // the (cached) result
        source.Gate.TrySetResult(true);
        Assert.True(firstPlacement.Task.Wait(TimeSpan.FromSeconds(30)), "first placement pass did not finish");
        var resultA = layer.GetPlacementResult(keyA, 8, (x, y) => (x, y));
        Assert.NotNull(resultA);

        // Viewport change: a new tile set (key B) has no result yet and its
        // pass runs in the background, blocked on a new gate. The previous
        // set must be handed out, not null: the symbols stay on the screen
        source.Gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Assert.Same(resultA, layer.GetPlacementResult(keyB, 8, (x, y) => (x, y)));

        // As soon as the new pass finishes, the new result replaces the old
        source.Gate.TrySetResult(true);
        secondPlacement.Task.Wait(TimeSpan.FromSeconds(30));
        var resultB = layer.GetPlacementResult(keyB, 8, (x, y) => (x, y));
        Assert.NotNull(resultB);
        Assert.NotSame(resultA, resultB);
    }
}
