using BruTile;
using Mapsui;

namespace VecTiles.Controls.Mapsui;

/// <summary>
/// Result of a fetch pass: the rendered tiles that could be fetched and a
/// flag telling whether at least one tile of the request is missing.
/// </summary>
/// <param name="Features">The fetched rendered tiles.</param>
/// <param name="Incomplete">True when at least one tile could not be fetched.</param>
public sealed record TileFetchResult(IReadOnlyList<RenderedTileFeature>? Features, bool Incomplete);

/// <summary>
/// Fetches the rendered tiles needed by the placement pass. The tiles are
/// fetched in parallel with a bounded degree of concurrency, the tiles
/// nearest to the center of the viewport first, so the center of the map
/// gets its symbols before the edges. The scheduler does not retry failed
/// fetches: the retry policy (no new attempt per frame, cooldown after an
/// incomplete pass) is handled by <see cref="RenderedSymbolsLayer"/>.
/// </summary>
public sealed class TileFetchScheduler
{
    private readonly IRenderedTileSource _tileSource;

    private readonly int _maxConcurrentFetches;

    /// <summary>
    /// Initializes a new instance of the <see cref="TileFetchScheduler"/> class.
    /// </summary>
    /// <param name="tileSource">The rendered tile source to fetch from.</param>
    /// <param name="maxConcurrentFetches">The maximum number of tiles fetched in parallel.</param>
    public TileFetchScheduler(IRenderedTileSource tileSource, int maxConcurrentFetches = 4)
    {
        _tileSource = tileSource;
        _maxConcurrentFetches = maxConcurrentFetches;
    }

    /// <summary>
    /// Fetches all given tiles. Tiles that cannot be fetched are skipped and
    /// reported via <see cref="TileFetchResult.Incomplete"/>; a single
    /// missing or failing tile does not fail the whole pass.
    /// </summary>
    /// <param name="tileInfos">The tiles to fetch.</param>
    /// <param name="centerWorld">
    /// The center of the viewport in world coordinates; tiles are ordered by
    /// their distance to this point. Null keeps the order of the input.
    /// </param>
    /// <returns>The fetched tiles.</returns>
    public async Task<TileFetchResult> FetchAsync(IReadOnlyList<TileInfo> tileInfos, MPoint? centerWorld = null)
    {
        var ordered = OrderByPriority(tileInfos, centerWorld);

        using var semaphore = new SemaphoreSlim(_maxConcurrentFetches);

        var fetched = await Task.WhenAll(ordered.Select(async tileInfo =>
        {
            await semaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                return (RenderedTileFeature?)await _tileSource.GetTileAsync(tileInfo).ConfigureAwait(false);
            }
            catch
            {
                // A tile that fails to load stays missing in the placement;
                // it must not fail pass over all the other tiles
                return null;
            }
            finally
            {
                semaphore.Release();
            }
        })).ConfigureAwait(false);

        var features = fetched.Where(f => f is not null).Cast<RenderedTileFeature>().ToList();

        return new TileFetchResult(features, features.Count < tileInfos.Count);
    }

    private static IEnumerable<TileInfo> OrderByPriority(IReadOnlyList<TileInfo> tileInfos, MPoint? centerWorld)
    {
        if (centerWorld is null || tileInfos.Count < 2)
        {
            return tileInfos;
        }

        return tileInfos
            .OrderBy(tileInfo => SquaredDistance(tileInfo.Extent.CenterX, tileInfo.Extent.CenterY,
                centerWorld.X, centerWorld.Y));
    }

    private static double SquaredDistance(double x1, double y1, double x2, double y2)
    {
        var dx = x1 - x2;
        var dy = y1 - y2;
        return dx * dx + dy * dy;
    }
}
