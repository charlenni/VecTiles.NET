using BruTile;
using VecTiles.Common.Primitives;
using VecTiles.Renderers.Common;
using VecTiles.Renderers.Common.Interfaces;

namespace VecTiles.Renderers.Skia;

/// <summary>
/// Snapshot for one frame: the tiles to draw and the placement result for
/// the current tile set and zoom level. <see cref="Placement"/> is never
/// null: while the placement is being computed (or nothing was placed yet)
/// it is an empty result, so the drawing code needs no null check.
/// </summary>
/// <param name="Tiles">
/// The rendered tiles that have arrived for the current view, in tile id
/// order.
/// </param>
/// <param name="Placement">The placement result of the current view.</param>
public sealed record PlacementView(IReadOnlyList<TileInView> Tiles, PlacementResult Placement);

/// <summary>
/// One rendered tile of the current view together with the tile info of the
/// request (the tile info carries the world extent of the tile, which the
/// drawing needs for the per-tile transform).
/// </summary>
/// <param name="Info">The tile info of the fetch request.</param>
/// <param name="Tile">The rendered tile.</param>
public sealed record TileInView(TileInfo Info, IRenderedTile Tile);

/// <summary>
/// The placement machinery behind a map layer: fetch of the rendered tiles
/// and the background placement pass over them. All state of the pipeline
/// lives here; the map layer only converts the viewport and draws.
/// </summary>
/// <remarks>
/// Responsibilities of the parts:
/// <list type="bullet">
/// <item>Tile fetching starts immediately on a viewport change (center
/// first, see <see cref="TileFetchScheduler"/>); every arrived tile raises
/// <see cref="TileArrived"/>, so the map redraws with the new geometry.</item>
/// <item>The placement pass starts when the zoom has been stable for
/// <see cref="PlacementDebounceMs"/> (during a continuous zoom change every
/// change would cancel the previous pass anyway). Its tile fetching reuses
/// the tiles of the fetch pass (cache hits). A finished pass raises
/// <see cref="PlacementCompleted"/>, so the map redraws with the symbols.</item>
/// <item>A zoom change cancels the running pass. The retained set of the
/// previous zoom stays on the screen as a holdover: its symbols are drawn
/// with the style of the current view (line runs rescaled into the current
/// placement space) until the new pass finishes and replaces them. At the
/// same zoom (panning) the retained set is handed out unchanged, so the
/// symbols never disappear in between.</item>
/// <item>Finished results are kept per tile set and zoom level, so
/// returning to a recent viewport does not recompute.</item>
/// <item>The placement decisions are persistent per zoom level (see
/// <see cref="IncrementalPlacementState"/>): once placed, a label keeps
/// its position, also when tiles of the viewport change.</item>
/// </list>
/// </remarks>
public sealed class PlacementService
{
    /// <summary>
    /// Minimum time in milliseconds between two placement passes over the
    /// same tile set whose result is incomplete: as long as a tile stays
    /// unfetchable, the next frames must not start a new pass each
    /// (retry spin against a permanently unavailable source). New tiles
    /// arriving for the same view bypass the cooldown: they make the next
    /// pass meaningful (more data to place), so the result of the current
    /// zoom replaces the holdover of the previous zoom as soon as possible.
    /// </summary>
    private const int IncompleteRetryCooldownMs = 2000;

    private const int MaxCachedPlacements = 128;

    private readonly object _lock = new();

    private readonly TileFetchScheduler _fetchScheduler;

    private readonly Dictionary<(TilesKey Tiles, int Zoom), PlacementResult?> _placements = new();
    private readonly HashSet<(TilesKey Tiles, int Zoom)> _inFlight = new();

    /// <summary>
    /// The last attempt per view that produced an incomplete result: the
    /// time it was stored and the number of view tiles it ran over. A new
    /// attempt waits for the cooldown or for new tiles to arrive, whichever
    /// comes first (see <see cref="IncompleteRetryCooldownMs"/>).
    /// </summary>
    private readonly Dictionary<(TilesKey Tiles, int Zoom), (long Tick, int TileCount)> _incompleteAttempts = new();

    /// <summary>
    /// The tiles fetched for the current view by tile id (renderer results
    /// are keyed by the tile index, so duplicate fetches of the same tile
    /// within one view overwrite each other instead of adding twice).
    /// </summary>
    private readonly Dictionary<ulong, TileInView> _viewTiles = new();

    /// <summary>
    /// The view the placement was started for; a change cancels the run in
    /// progress and starts the next one.
    /// </summary>
    private (TilesKey Tiles, int Zoom)? _currentKey;

    private CancellationTokenSource? _viewCts;
    private CancellationTokenSource? _runCts;
    private CancellationTokenSource? _debounceCts;

    /// <summary>
    /// The last placement result that was handed out. It keeps the current
    /// symbols on the screen while a new placement is computed (for a
    /// changed tile set after panning, for example), so the symbols never
    /// disappear in between: the old set stays until the new set exists.
    /// The result is bound to a zoom level (<see cref="PlacementResult.ZoomLevel"/>);
    /// after a zoom change it is handed out as a rescaled holdover until
    /// the placement of the new zoom replaces it.
    /// </summary>
    private PlacementResult _lastResult = PlacementResult.Empty();

    private int _lastZoom = -1;

    /// <summary>
    /// Incremented by <see cref="ClearCache"/>; results of background tasks
    /// started before a clear are no longer stored.
    /// </summary>
    private int _generation;

    /// <summary>
    /// Raised for every tile that arrived for the current view (from a fetch
    /// task), so the map can redraw with the arrived geometry.
    /// </summary>
    public event Action<IRenderedTile>? TileArrived;

    /// <summary>
    /// Raised after a placement pass stored its result, so the map can
    /// redraw with the placed symbols.
    /// </summary>
    public event Action? PlacementCompleted;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlacementService"/> class.
    /// </summary>
    /// <param name="tileSource">The rendered tile source to fetch from.</param>
    /// <param name="maxConcurrentFetches">The maximum number of tiles fetched in parallel.</param>
    public PlacementService(IRenderedTileSource tileSource, int maxConcurrentFetches = 4)
    {
        _fetchScheduler = new TileFetchScheduler(tileSource, maxConcurrentFetches);
    }

    /// <summary>
    /// Time in milliseconds the zoom must stay constant before the placement
    /// pass starts. While the zoom keeps changing every pass would be
    /// cancelled by the next change anyway.
    /// </summary>
    public int PlacementDebounceMs { get; set; } = 250;

    /// <summary>
    /// Gets or sets a value indicating whether the placement pass runs
    /// synchronously and blocking inside <see cref="Update"/> instead of in
    /// a background task. Used by tests and tooling.
    /// </summary>
    public bool SynchronousPlacement { get; set; }

    /// <summary>
    /// Gets or sets the placement of the current view: fetches the tiles (a
    /// changed view starts its fetch immediately) and returns the arrived
    /// tiles together with the placement result. The placement result is
    /// never null: until a new pass has finished for the current zoom, the
    /// retained set is handed out — the same zoom unchanged (panning keeps
    /// the symbols on the screen), the previous zoom rescaled as a holdover
    /// (the labels stay on the screen with the style of the current view)
    /// — or an empty result, so no symbols are drawn.
    /// </summary>
    /// <param name="tileInfos">The tile infos of the current view.</param>
    /// <param name="zoomLevel">The canonical integer zoom level.</param>
    /// <param name="worldToScreen">
    /// Converts world coordinates into the unrotated canonical placement space
    /// the collision checks and placements are evaluated in.
    /// </param>
    /// <param name="centerWorld">
    /// The center of the viewport in world coordinates; tiles near the center
    /// are fetched first. Null keeps the order of the input.
    /// </param>
    /// <param name="screenTileInfos">
    /// The tiles that really intersect the screen (the subset of
    /// <paramref name="tileInfos"/> visible on screen). A missing screen tile
    /// keeps the result incomplete (the holdover stays on the screen until
    /// the tile arrives); a missing tile outside this set (the padding ring
    /// of a tile source with a limited coverage, e.g. a local MBTiles file)
    /// no longer blocks the publication of the placement — its symbols are
    /// placed from what arrived, which is complete inside the screen. Null
    /// treats every tile of the view as a screen tile (the old behavior).
    /// </param>
    /// <returns>The arrived tiles and the placement result of the view.</returns>
    public PlacementView Update(IReadOnlyList<TileInfo> tileInfos, int zoomLevel,
        Func<double, double, (double, double)> worldToScreen, (double X, double Y)? centerWorld = null,
        IReadOnlyList<TileInfo>? screenTileInfos = null)
    {
        var infoArray = tileInfos as TileInfo[] ?? [.. tileInfos];
        var key = (new TilesKey(infoArray.ToTile()), zoomLevel);

        var runSynchronously = false;

        lock (_lock)
        {
            if (zoomLevel != _lastZoom)
            {
                // The retained set belongs to the old zoom, but it stays on
                // the screen as a holdover: its symbols are drawn with the
                // style and positions of the current view (CurrentPlacement
                // rescales the line runs), until the placement of the new
                // zoom replaces them. A brief overlap of the two symbol sets
                // is far less noticeable than labels that vanish and come
                // back
                _lastZoom = zoomLevel;
            }

            if (key != _currentKey)
            {
                var previousKey = _currentKey;
                _currentKey = key;

                // A change of the view stops the debounce timer, the fetch
                // pass and the placement run of the old view in progress
                _debounceCts?.Cancel();
                _debounceCts?.Dispose();
                _runCts?.Cancel();
                _runCts?.Dispose();
                _viewCts?.Cancel();
                _viewCts?.Dispose();
                _viewCts = new CancellationTokenSource();
                _runCts = new CancellationTokenSource();
                _debounceCts = new CancellationTokenSource();

                RetainTilesOnViewChange(previousKey, key, infoArray);

                if (SynchronousPlacement)
                {
                    // The blocking pass must run outside the lock: its fetch
                    // callbacks store arrived tiles under the lock again
                    runSynchronously = true;
                }
                else
                {
                    // Geometry first: the fetch of the tiles starts
                    // immediately, not debounced (every arrived tile raises
                    // TileArrived and redraws the map)
                    StartFetchPass(infoArray, key, centerWorld, _viewCts!.Token);

                    // Symbols follow, once the zoom has settled
                    StartDelayedRun(infoArray, key, zoomLevel, worldToScreen, centerWorld, _debounceCts!.Token,
                        _runCts.Token, screenTileInfos);
                }
            }
            else if (_placements.TryGetValue(key, out var incompleteCached)
                && incompleteCached is { Incomplete: true }
                && _incompleteAttempts.TryGetValue(key, out var lastAttempt)
                && (Environment.TickCount64 - lastAttempt.Tick >= IncompleteRetryCooldownMs
                    || _viewTiles.Count > lastAttempt.TileCount))
            {
                // The tile set stays incomplete (a tile cannot be fetched):
                // a new attempt only after the cooldown — or as soon as new
                // tiles have arrived since the last attempt, because then
                // the next pass has more data and the result of the current
                // zoom can replace the holdover of the previous zoom early
                _incompleteAttempts[key] = (Environment.TickCount64, _viewTiles.Count);

                if (SynchronousPlacement)
                {
                    runSynchronously = true;
                }
                else
                {
                    _runCts?.Cancel();
                    _runCts?.Dispose();
                    _runCts = new CancellationTokenSource();

                    StartRun(infoArray, key, zoomLevel, worldToScreen, centerWorld, _runCts.Token, _generation,
                        screenTileInfos);
                }
            }

            if (runSynchronously)
            {
                // Blocks until the pass over the current view has finished;
                // used by tests and tooling
                _viewCts?.Dispose();
                _viewCts = null;
            }
        }

        if (runSynchronously)
        {
            // Outside the lock: callbacks of the pass (AddTileToView) take
            // the lock themselves
            RunPass(infoArray, key, zoomLevel, worldToScreen, centerWorld, CancellationToken.None,
                screenTileInfos).GetAwaiter().GetResult();
        }

        lock (_lock)
        {
            // Snapshot for the drawing phase: the arrived tiles of the
            // current view and the placement result
            _placements.TryGetValue(key, out var cached);
            var tiles = _viewTiles.Count == 0
                ? new List<TileInView>()
                : _viewTiles.Values.OrderBy(t => t.Tile.Tile.Id).ToList();

            var placement = CurrentPlacement(key, zoomLevel, cached);

            TrimPlacementCache();

            return new PlacementView(tiles, placement);
        }
    }

    /// <summary>
    /// Rebuilds the arrived tiles of the view after a view change: a pan
    /// keeps the tiles that still belong to the new tile set, so they draw
    /// immediately without waiting for the fetch pass (the map does not
    /// flicker while panning). A zoom change keeps nothing: tiles of another
    /// zoom level are not drawn.
    /// </summary>
    private void RetainTilesOnViewChange((TilesKey Tiles, int Zoom)? previousKey, (TilesKey Tiles, int Zoom) key,
        IReadOnlyList<TileInfo> tileInfos)
    {
        if (_viewTiles.Count == 0)
        {
            return;
        }

        if (previousKey is null || previousKey.Value.Zoom != key.Zoom)
        {
            _viewTiles.Clear();
            return;
        }

        var keep = new HashSet<TileIndex>(tileInfos.Select(info => info.Index));
        var retained = _viewTiles.Values.Where(tileInView => keep.Contains(tileInView.Info.Index)).ToList();

        _viewTiles.Clear();
        foreach (var tileInView in retained)
        {
            _viewTiles[tileInView.Tile.Tile.Id] = tileInView;
        }
    }

    /// <summary>
    /// Clears the cached placements, so they are recomputed on next access.
    /// Background tasks that are still running discard their results.
    /// </summary>
    public void ClearCache()
    {
        lock (_lock)
        {
            _placements.Clear();
            _inFlight.Clear();
            _incompleteAttempts.Clear();
            _viewTiles.Clear();
            // The retired symbol set is invalidated too: after an explicit
            // clear (style reload) nothing obsolete is drawn anymore
            _lastResult = PlacementResult.Empty();
            _lastZoom = -1;
            _holdoverSource = null;
            _holdoverZoom = -1;
            _holdoverResult = PlacementResult.Empty();
            // The persistent decisions belong to the old content as well
            _incrementalStates.Clear();
            // Reset the current view: the next Update must start a new fetch
            // pass over the (cleared) view tiles even when the tile set has
            // not changed — otherwise a style change clears the tiles from
            // the view and nothing would be fetched or drawn until the user
            // moves the map
            _currentKey = null;
            _generation++;
        }
    }

    /// <summary>
    /// The placement result of the current call: a complete cached result,
    /// otherwise the retained set of the same zoom as holdover, otherwise
    /// the retained set of the previous zoom (rescaled into the current
    /// placement space, so labels stay on their lines), otherwise an empty
    /// result (the drawing needs no null check).
    /// </summary>
    private PlacementResult CurrentPlacement((TilesKey Tiles, int Zoom) key, int zoomLevel, PlacementResult? cached)
    {
        // Complete results are handed out directly; they also become the
        // retained set for the time of the next computation
        if (cached is { Incomplete: false })
        {
            _lastResult = cached;
            return cached;
        }

        if (_lastResult.ZoomLevel != zoomLevel)
        {
            // Holdover of the previous zoom: hand out a rescaled copy marked
            // for the current zoom (one copy per source result and target
            // zoom, so the drawing phase receives a stable reference and the
            // frame keys again). Incomplete keeps the retained frame
            // underlay active while tiles of the new zoom are missing
            if (!ReferenceEquals(_holdoverSource, _lastResult) || _holdoverZoom != zoomLevel)
            {
                _holdoverResult = _lastResult.Rescaled(zoomLevel) with
                {
                    Incomplete = true,
                    IsHoldover = true,
                };
                _holdoverSource = _lastResult;
                _holdoverZoom = zoomLevel;
            }

            return _holdoverResult;
        }

        // The result is being computed (or stayed incomplete): the retained
        // set of the same zoom keeps the symbols on the screen
        return _lastResult;
    }

    // The holdover handed out while the placement of a new zoom runs: a
    // rescaled copy of the retained set of the previous zoom
    private PlacementResult? _holdoverSource;
    private int _holdoverZoom = -1;
    private PlacementResult _holdoverResult = PlacementResult.Empty();

    /// <summary>
    /// The persistent placement decisions per zoom level: the collision tree
    /// and the placed/rejected outcome of every examined symbol survive the
    /// passes, so labels never move when tiles appear or disappear (see
    /// <see cref="IncrementalPlacementState"/>).
    /// </summary>
    private readonly Dictionary<int, IncrementalPlacementState> _incrementalStates = new();

    private IncrementalPlacementState StateFor(int zoomLevel)
    {
        lock (_lock)
        {
            if (!_incrementalStates.TryGetValue(zoomLevel, out var state))
            {
                state = new IncrementalPlacementState();
                _incrementalStates[zoomLevel] = state;
            }

            return state;
        }
    }

    /// <summary>
    /// Fetches the tiles of the current view immediately (center first via
    /// the scheduler); every arrived tile updates the view snapshot and
    /// raises <see cref="TileArrived"/>.
    /// </summary>
    private void StartFetchPass(IReadOnlyList<TileInfo> tileInfos, (TilesKey Tiles, int Zoom) key,
        (double X, double Y)? centerWorld, CancellationToken token)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await _fetchScheduler.FetchAsync(tileInfos, centerWorld, token,
                    (info, tile) => AddTileToView(key, info, tile)).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // The view changed; the pass of the new view is running
            }
            catch
            {
                // A failing fetch pass stays incomplete; the placement run
                // of the same tiles reports the incompleteness
            }
        });
    }

    /// <summary>
    /// Delays the placement pass until the zoom has been constant for
    /// <see cref="PlacementDebounceMs"/>, then starts it.
    /// </summary>
    private void StartDelayedRun(IReadOnlyList<TileInfo> tileInfos, (TilesKey Tiles, int Zoom) key, int zoomLevel,
        Func<double, double, (double, double)> worldToScreen, (double X, double Y)? centerWorld,
        CancellationToken debounceToken, CancellationToken runToken, IReadOnlyList<TileInfo>? screenTileInfos)
    {
        var generation = _generation;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(PlacementDebounceMs, debounceToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // The view changed before the zoom settled
                return;
            }

            await StartRun(tileInfos, key, zoomLevel, worldToScreen, centerWorld, runToken, generation,
                screenTileInfos)
                .ConfigureAwait(false);
        });
    }

    /// <summary>
    /// Runs one placement pass: (re)fetches the tiles of the pass (mostly
    /// cache hits of the tile fetch pass in progress) and computes the
    /// placement. The result is stored under the key of the view; a result
    /// that was superseded by a change of the view (cancelled token) or by
    /// a <see cref="ClearCache"/> is discarded.
    /// </summary>
    private async Task StartRun(IReadOnlyList<TileInfo> tileInfos, (TilesKey Tiles, int Zoom) key, int zoomLevel,
        Func<double, double, (double, double)> worldToScreen, (double X, double Y)? centerWorld,
        CancellationToken token, int generation, IReadOnlyList<TileInfo>? screenTileInfos = null)
    {
        PlacementResult? result = null;

        try
        {
            var fetch = await _fetchScheduler.FetchAsync(tileInfos, centerWorld, token,
                (info, tile) => AddTileToView(key, info, tile)).ConfigureAwait(false);

            result = ComputePlacement(fetch, key.Zoom, worldToScreen, token, StateFor(key.Zoom),
                screenTileInfos);
        }
        catch (OperationCanceledException)
        {
            // The run was cancelled (zoom change or new tile set): its result
            // must not be stored, it belongs to a view that no longer exists
        }
        catch
        {
            // Leave the last result in place; the placement is recomputed on
            // the next viewport change
        }

        if (result is null)
        {
            return;
        }

        var fire = false;

        lock (_lock)
        {
            if (_generation == generation && key == _currentKey)
            {
                _placements[key] = result;
                if (result.Incomplete)
                {
                    // The retry cooldown of an incomplete result starts when
                    // the result is stored, not on the next call; the tile
                    // count lets a later retry start as soon as new tiles
                    // have arrived
                    _incompleteAttempts[key] = (Environment.TickCount64, _viewTiles.Count);
                }
                else
                {
                    // A complete result clears the cooldown history and
                    // becomes the retained set
                    _incompleteAttempts.Remove(key);
                    _lastResult = result;
                    _lastZoom = key.Zoom;
                }

                fire = true;
            }
        }

        if (fire)
        {
            // Draw the newly placed symbols once, without the caller having
            // to move the viewport
            PlacementCompleted?.Invoke();

            if (result.Incomplete)
            {
                // The pass ran over an incomplete tile set and the retained
                // set belongs to another zoom (holdover on the screen). If
                // the zooming has stopped and every tile is decided, no
                // Update runs anymore and no new pass would ever start: the
                // labels of the last placed view would stay on the screen
                // until the user redraws. Schedule one retry after the
                // cooldown, so the holdover is replaced on its own
                ScheduleHoldoverRetry(key, token);
            }
        }
    }

    /// <summary>
    /// Requests a new placement pass for a view whose last pass ended with
    /// an incomplete result while the symbols of another zoom are still on
    /// the screen: after the cooldown the completion event triggers a redraw
    /// whose <see cref="Update"/> call starts the pass (the cooldown has
    /// elapsed by then). Repeats itself while the result stays incomplete,
    /// stops as soon as the pass completes, the view changes or the cache is
    /// cleared.
    /// </summary>
    private void ScheduleHoldoverRetry((TilesKey Tiles, int Zoom) key, CancellationToken token)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(IncompleteRetryCooldownMs, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            bool fire;

            lock (_lock)
            {
                fire = key == _currentKey
                    && _placements.TryGetValue(key, out var incomplete) && incomplete is { Incomplete: true }
                    && _lastResult.ZoomLevel != key.Zoom;
            }

            if (fire)
            {
                PlacementCompleted?.Invoke();
            }
        });
    }

    /// <summary>
    /// Runs the fetch and the placement pass synchronously (tests and
    /// tooling) and stores the result.
    /// </summary>
    private async Task RunPass(IReadOnlyList<TileInfo> tileInfos, (TilesKey Tiles, int Zoom) key, int zoomLevel,
        Func<double, double, (double, double)> worldToScreen, (double X, double Y)? centerWorld,
        CancellationToken token, IReadOnlyList<TileInfo>? screenTileInfos = null)
    {
        try
        {
            var fetch = await _fetchScheduler.FetchAsync(tileInfos, centerWorld, token,
                (info, tile) => AddTileToView(key, info, tile)).ConfigureAwait(false);

            var result = ComputePlacement(fetch, key.Zoom, worldToScreen, token, StateFor(key.Zoom),
                screenTileInfos);

            lock (_lock)
            {
                _placements[key] = result;
                if (result.Incomplete)
                {
                    // The retry cooldown of an incomplete result starts when
                    // the result is stored, not on the next call; the tile
                    // count lets a later retry start as soon as new tiles
                    // have arrived
                    _incompleteAttempts[key] = (Environment.TickCount64, _viewTiles.Count);
                }
                else
                {
                    _incompleteAttempts.Remove(key);
                }

                // Only a complete result replaces the retained set: an
                // incomplete pass (fast zooming, so the pass ran over few or
                // no arrived tiles) must not throw the holdover away,
                // otherwise every symbol vanishes until a full pass finishes
                // and the crossfade would never start
                if (!result.Incomplete)
                {
                    _lastResult = result;
                    _lastZoom = key.Zoom;
                }
            }
        }
        catch
        {
            // A failing pass leaves the last result in place
        }
    }

    /// <summary>
    /// Runs the placement engine over the fetched tiles of the pass. With an
    /// incremental state the decisions of earlier passes (per zoom) persist:
    /// labels stay where the pass that first placed them put them, also when
    /// the tile set of the viewport changes.
    /// </summary>
    /// <param name="fetch">The tiles the pass fetched and whether any is missing.</param>
    /// <param name="zoomLevel">The canonical zoom level of the pass.</param>
    /// <param name="worldToScreen">World to placement-space projection.</param>
    /// <param name="token">Cancels the pass.</param>
    /// <param name="incremental">The persistent state of the zoom level.</param>
    /// <param name="screenTileInfos">
    /// The tiles that really intersect the screen. Only a missing screen tile
    /// keeps the result incomplete — a missing padding tile (a source with a
    /// limited coverage) must not keep the placement from being published.
    /// </param>
    private static PlacementResult ComputePlacement(TileFetchResult fetch, int zoomLevel,
        Func<double, double, (double, double)> worldToScreen, CancellationToken token,
        IncrementalPlacementState incremental, IReadOnlyList<TileInfo>? screenTileInfos = null)
    {
        var blocksPublication = BlocksPublication(fetch, screenTileInfos);

        if (fetch.Tiles is null || fetch.Tiles.Count == 0)
        {
            // Viewport contains no tiles or there is no information for these tiles
            return new PlacementResult
            {
                PlacedByLayer = new Dictionary<string, List<PlacementEntry>>(),
                Incomplete = blocksPublication,
                ZoomLevel = zoomLevel,
            };
        }

        // An incomplete pass sees only part of the tiles of the viewport; its
        // result is not published (see the latch), so its decisions must not
        // enter the persistent state either — the pass that sees the complete
        // tile set decides and pins the labels
        var placed = blocksPublication
            ? SymbolPlacementEngine.Compute(fetch.Tiles, zoomLevel, worldToScreen, token, null) with { Incomplete = true }
            : SymbolPlacementEngine.Compute(fetch.Tiles, zoomLevel, worldToScreen, token, incremental);

        return placed;
    }

    /// <summary>
    /// True when the placement result of this fetch must not replace the
    /// retained set of the view: a screen tile is missing (its symbols may
    /// still be missing), so the holdover stays on the screen until the tile
    /// arrives. Missing tiles outside the screen set (the padding ring of a
    /// tile source with a limited coverage) do not block: everything the
    /// user sees is placed from complete data. Without a screen tile set
    /// every missing tile blocks (the old behavior).
    /// </summary>
    private static bool BlocksPublication(TileFetchResult fetch, IReadOnlyList<TileInfo>? screenTileInfos)
    {
        if (!fetch.Incomplete)
        {
            return false;
        }

        if (screenTileInfos is null)
        {
            return true;
        }

        var missing = fetch.MissingTiles;
        if (missing is null)
        {
            // Older TileFetchResult without the missing tile report: assume
            // the worst (a missing tile may be a screen tile)
            return true;
        }

        var screenIndexes = screenTileInfos.Select(info => info.Index).ToHashSet();

        return missing.Any(info => screenIndexes.Contains(info.Index));
    }

    /// <summary>
    /// Store an arrived tile in the view snapshot of its view (under the
    /// lock: fetch tasks run in parallel) and raise <see cref="TileArrived"/>
    /// outside the lock. A tile of a pass that no longer belongs to the
    /// current view (cancelled on a view change, its completion arrives
    /// late) is ignored.
    /// </summary>
    private void AddTileToView((TilesKey Tiles, int Zoom) key, TileInfo info, IRenderedTile tile)
    {
        Action<IRenderedTile>? raise;

        lock (_lock)
        {
            if (_currentKey != key || _viewTiles.ContainsKey(tile.Tile.Id))
            {
                return;
            }

            _viewTiles[tile.Tile.Id] = new TileInView(info, tile);
            raise = TileArrived;
        }

        raise?.Invoke(tile);
    }

    private void TrimPlacementCache()
    {
        if (_placements.Count <= MaxCachedPlacements)
        {
            return;
        }

        foreach (var cacheKey in _placements.Keys.ToList())
        {
            if (_placements.Count <= MaxCachedPlacements)
            {
                return;
            }

            if (!_inFlight.Contains(cacheKey))
            {
                _placements.Remove(cacheKey);
            }
        }

        // Keep the cooldown history aligned with the placement cache
        foreach (var cacheKey in _incompleteAttempts.Keys.ToList())
        {
            if (!_placements.ContainsKey(cacheKey))
            {
                _incompleteAttempts.Remove(cacheKey);
            }
        }
    }
}
