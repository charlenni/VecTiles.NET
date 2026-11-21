using BruTile;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Rendering.Skia;
using Mapsui.Tiling.Extensions;
using System.Linq;
using VecTiles.Common.Primitives;
using VecTiles.Controls.Mapsui.Extensions;
using VecTiles.Renderers.Common.Interfaces;
using VecTiles.Renderers.Skia;
using VecTiles.Renderers.Skia.Primitives;

namespace VecTiles.Controls.Mapsui;

/// <summary>
/// A Mapsui layer that renders symbols from a rendered tile source.
/// </summary>
public class RenderedSymbolsLayer : BaseLayer
{
    private const string _customLayerRendererName = "rendered-symbols-layer";

    private const int MaxCachedPlacements = 128;

    /// <summary>
    /// Minimum time in milliseconds between two placement passes over the
    /// same tile set whose result is incomplete: as long as a tile stays
    /// unfetchable, the next frames must not start a new pass each
    /// (retry spin against a permanently unavailable source).
    /// </summary>
    private const int IncompleteRetryCooldownMs = 2000;

    private readonly MRect? _extent;

    private readonly object _placementLock = new();
    private readonly Dictionary<(TilesKey Tiles, int ZoomLevel), PlacementResult?> _placements = new();
    private readonly HashSet<(TilesKey Tiles, int ZoomLevel)> _inFlight = new();
    private readonly Dictionary<(TilesKey Tiles, int ZoomLevel), long> _incompleteAttempts = new();
    private readonly TileFetchScheduler _fetchScheduler;

    /// <summary>
    /// The last placement result that was handed out. It keeps the current
    /// symbols on the screen while a new placement is computed (for a
    /// changed tile set after panning, for example), so the symbols never
    /// disappear in between: the old set stays until the new set exists.
    /// </summary>
    private PlacementResult? _lastResult;

    /// <summary>
    /// Incremented by <see cref="ClearCache"/>; results of background tasks
    /// started before a clear are no longer stored.
    /// </summary>
    private int _generation;

    /// <summary>
    /// Initializes a new instance of the <see cref="RenderedSymbolsLayer"/> class.
    /// </summary>
    /// <param name="tileSource">The rendered tile source providing the symbols.</param>
    /// <param name="fontSourceResolver">
    /// Optional resolver that maps a font name (as in the style file, e.g.
    /// "Noto Sans Bold") to a source in URL notation (file://, https://,
    /// embedded://, ...), from which the font file is loaded via
    /// <see cref="VecTiles.DataSources.ByteDataSource.GetBytesAsync"/> when
    /// the font is neither registered nor installed on the system. Returning
    /// null or an empty string leaves the font to the usual resolution order.
    /// The resolver is held globally by <see cref="FontCache"/>; the resolver
    /// of the last created layer wins.
    /// </param>
    public RenderedSymbolsLayer(IRenderedTileSource tileSource, Func<string, string?>? fontSourceResolver = null)
    {
        MapRenderer.RegisterLayerRenderer(_customLayerRendererName, RenderedSymbolsLayerRenderer.Draw);

        TileSource = tileSource;
        CustomLayerRendererName = _customLayerRendererName;
        _fetchScheduler = new TileFetchScheduler(tileSource);

        FontSourceResolver = fontSourceResolver;
        if (fontSourceResolver != null)
        {
            FontCache.SetFontSourceResolver(fontSourceResolver);
        }

        _extent = TileSource.Schema.Extent.ToMRect();
    }

    /// <summary>
    /// Gets the tile source for this layer.
    /// </summary>
    public IRenderedTileSource TileSource { get; private set; }

    /// <summary>
    /// Gets the optional resolver that maps a font name to a source from
    /// which the font file is loaded when the font is missing. Null if no
    /// resolver was passed to the constructor.
    /// </summary>
    public Func<string, string?>? FontSourceResolver { get; }

    /// <inheritdoc />
    public override IReadOnlyList<double> Resolutions => TileSource.Schema.Resolutions.Select(r => r.Value.UnitsPerPixel).ToList();

    /// <inheritdoc />
    public override MRect? Extent => _extent;

    /// <summary>
    /// Gets or sets a value indicating whether to show valid symbol borders for debugging.
    /// </summary>
    public bool ShowValidBorders { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to show invalid symbol borders for debugging.
    /// </summary>
    public bool ShowInvalidBorders { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the placement pass runs
    /// synchronously and blocking inside <see cref="GetPlacementResult"/>
    /// instead of in a background task. Used by tests and tooling.
    /// </summary>
    public bool SynchronousPlacement { get; set; }

    /// <summary>
    /// Clears the cached placements, so they are recomputed on next access.
    /// Background tasks that are still running discard their results.
    /// </summary>
    public void ClearCache()
    {
        lock (_placementLock)
        {
            _placements.Clear();
            _inFlight.Clear();
            _incompleteAttempts.Clear();
            // The retired symbol set is invalidated too: after an explicit
            // clear (style reload) nothing obsolete is drawn anymore
            _lastResult = null;
            _generation++;
        }
    }

    /// <summary>
    /// Gets the placement result for the specified tile set and zoom level.
    /// The placement is computed at most once per key: on the first call the
    /// computation starts as a background task and the last handed-out
    /// result is returned (so the symbols stay on the screen until the new
    /// set exists); when the task finished, the (cached) new result is
    /// returned and the drawing phase is triggered again via
    /// <see cref="IDynamic.DataHasChanged"/>. Incomplete results (a tile
    /// could not be fetched) are returned as well, but a new computation is
    /// only started after the retry cooldown, so a permanently unavailable
    /// tile does not cause a fetch attempt per frame. With
    /// <see cref="SynchronousPlacement"/> the computation blocks until done.
    /// </summary>
    /// <param name="tileInfos">The tile information collection.</param>
    /// <param name="zoomLevel">The canonical integer zoom level.</param>
    /// <param name="worldToScreen">
    /// Converts world coordinates into the unrotated canonical placement space
    /// the collision checks and placements are evaluated in.
    /// </param>
    /// <param name="centerWorld">
    /// The center of the viewport in world coordinates; tiles near the center
    /// are fetched first. Null keeps the order of the input.
    /// </param>
    /// <returns>The placement result, or null while the placement is computed.</returns>
    public PlacementResult? GetPlacementResult(IEnumerable<TileInfo> tileInfos, int zoomLevel,
        Func<double, double, (double, double)> worldToScreen, MPoint? centerWorld = null)
    {
        var tileInfoArray = tileInfos as TileInfo[] ?? tileInfos.ToArray();
        var key = (new TilesKey(tileInfoArray.ToTile()), zoomLevel);

        lock (_placementLock)
        {
            _placements.TryGetValue(key, out var cached);

            // Complete results are returned directly; they also become the
            // retained set for the time of the next computation
            if (cached is { Incomplete: false })
            {
                _lastResult = cached;
                return cached;
            }

            if (_inFlight.Contains(key))
            {
                if (cached is not null)
                {
                    _lastResult = cached;
                    return cached;
                }

                // The current set is computed for the first time: keep the
                // previously handed-out set on the screen instead of letting
                // the symbols disappear
                return _lastResult;
            }

            // No retry spin on incomplete results: the same tile set within
            // the cooldown returns the last (incomplete) result without
            // starting a new fetch pass
            if (cached is not null && cached.Incomplete)
            {
                var now = Environment.TickCount64;
                if (_incompleteAttempts.TryGetValue(key, out var lastAttempt)
                    && now - lastAttempt < IncompleteRetryCooldownMs)
                {
                    _lastResult = cached;
                    return cached;
                }

                _incompleteAttempts[key] = now;
            }

            if (SynchronousPlacement)
            {
                var result = ComputePlacement(tileInfoArray, zoomLevel, worldToScreen, centerWorld).GetAwaiter().GetResult();
                _placements[key] = result;
                if (result.Incomplete)
                {
                    _incompleteAttempts[key] = Environment.TickCount64;
                }
                else
                {
                    _incompleteAttempts.Remove(key);
                }
                _lastResult = result;
                return result;
            }

            var generation = _generation;
            _placements[key] = cached is null ? null : cached;
            _inFlight.Add(key);

            var capturedScreen = worldToScreen;
            var capturedCenter = centerWorld;

            _ = Task.Run(async () =>
            {
                PlacementResult? result = null;

                try
                {
                    result = await ComputePlacement(tileInfoArray, zoomLevel, capturedScreen, capturedCenter).ConfigureAwait(false);
                }
                catch
                {
                    // Leave the last result in place; the placement is
                    // recomputed on the next viewport change
                }

                var fire = false;

                // Remove the key from the in-flight set and store the result
                // in the same locked section: a call in between saw no
                // in-flight entry and would have started a second pass
                lock (_placementLock)
                {
                    _inFlight.Remove(key);

                    if (_generation == generation && result is not null)
                    {
                        _placements[key] = result;
                        if (result.Incomplete)
                        {
                            // The retry cooldown of an incomplete result starts
                            // when the result is stored, not on the next call
                            _incompleteAttempts[key] = Environment.TickCount64;
                        }
                        else
                        {
                            // A complete result clears the cooldown history
                            _incompleteAttempts.Remove(key);
                        }
                        fire = true;
                    }
                }

                if (fire)
                {
                    // Draw the newly placed symbols once, without the caller
                    // having to move the viewport
                    DataHasChanged();
                }
            });

            TrimPlacementCache();

            // A new pass over this tile set started. Until it is finished,
            // the last handed-out result keeps the symbols on the screen
            if (cached is not null)
            {
                _lastResult = cached;
                return cached;
            }

            return _lastResult;
        }

        void TrimPlacementCache()
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

    /// <inheritdoc />
    public override IEnumerable<IFeature> GetFeatures(MRect box, double resolution)
    {
        return Array.Empty<IFeature>();
    }

    /// <summary>
    /// Fetches the tiles (via the fetch scheduler, in parallel and center
    /// first) and runs the placement pass over them.
    /// </summary>
    /// <param name="tileInfoArray">The tile information collection.</param>
    /// <param name="zoomLevel">The canonical integer zoom level.</param>
    /// <param name="worldToScreen">
    /// Converts world coordinates into the unrotated canonical placement space.
    /// </param>
    /// <param name="centerWorld">
    /// The center of the viewport in world coordinates; tiles near the center
    /// are fetched first. Null keeps the order of the input.
    /// </param>
    /// <returns>The placement result for all tiles.</returns>
    private async Task<PlacementResult> ComputePlacement(TileInfo[] tileInfoArray, int zoomLevel,
        Func<double, double, (double, double)> worldToScreen, MPoint? centerWorld)
    {
        var fetch = await _fetchScheduler.FetchAsync(tileInfoArray, centerWorld).ConfigureAwait(false);

        if (fetch.Features is null || fetch.Features.Count == 0)
        {
            // Viewport contains no tiles or there is no information for these tiles
            return new PlacementResult
            {
                PlacedByLayer = new Dictionary<string, List<PlacementEntry>>(),
                Incomplete = fetch.Incomplete,
            };
        }

        var placed = SymbolPlacementEngine.Compute(fetch.Features!.Select(f => f.RenderedTile).ToList(), zoomLevel, worldToScreen);
        return fetch.Incomplete ? placed with { Incomplete = true } : placed;
    }
}
