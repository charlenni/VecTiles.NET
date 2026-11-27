using BruTile;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Manipulations;
using Mapsui.Rendering;
using Mapsui.Rendering.Skia;
using Mapsui.Extensions;
using Mapsui.Tiling.Extensions;
using SkiaSharp;
using System.Linq;
using Mapsui.Widgets.ButtonWidgets;
using VecTiles.Controls.Mapsui.Extensions;
using VecTiles.Common.Primitives;
using VecTiles.Renderers.Common.Interfaces;
using VecTiles.Renderers.Skia;
using VecTiles.Renderers.Skia.Primitives;

namespace VecTiles.Controls.Mapsui;

/// <summary>
/// The map layer for rendered tiles: it displays the geometry and the
/// symbols of all style layers of the rendered tiles. The geometry of the
/// tiles is drawn immediately (stile layer by style layer across all visible
/// tiles); the symbols are only drawn when a placement result exists — the
/// placement pass runs in the background (see <see cref="PlacementService"/>)
/// and the layer redraws once the new symbol set is ready.
/// </summary>
public class RenderedTileLayer : BaseLayer, ILayerFeatureInfo
{
    private const string _customLayerRendererName = "rendered-tile-layer";

    private readonly PlacementService _placementService;
    private readonly MRect? _extent;

    // The offscreen surface the layer content is rasterized into before it
    // is drawn onto the screen canvas (one blit, so a cleared screen is
    // never visible while the layer draws), reused across frames. Rebuilt
    // when the viewport size changes. On a GPU-backed screen canvas it is a
    // GPU surface of the same GRContext, so rasterizing and blitting stay
    // on the GPU.
    private SKSurface? _frameSurface;
    private GRContext? _frameGrContext;

    // The last completely drawn frame; drawn 1:1 when nothing changed, so
    // the expensive style rasterization is skipped completely
    private SKImage? _retainedFrame;
    private SKRect _retainedFrameRect;
    private object? _retainedFrameKey;

    // The geometry-only frame of the last fully drawn state (same raster
    // pass, but drawn without the placed symbols): it is mapped into
    // changed viewports while tiles are missing. The symbols of the old
    // zoom would show scaled (double size when zooming in) inside such
    // an image, so the mapped draws use the geometry-only frame instead;
    // symbols are only ever drawn from a placement of the current zoom
    private SKImage? _retainedGeometryFrame;
    private SKRect _retainedGeometryFrameRect;

    // The viewport the geometry-only frame was drawn for (same mechanism)
    private (double CenterX, double CenterY, double Resolution, double Rotation,
        double Width, double Height)? _retainedGeometryFrameViewport;

    // The viewport of the previous draw; equal viewports in consecutive draws
    // mean the map stands still (only the content changed, e.g. the
    // placement completed), so those frames are rasterized offscreen and
    // blitted atomically instead of drawn directly
    private object? _lastViewportSignature;

    // Placement crossfade: when the holdover of the previous zoom is
    // replaced by the finished placement of the new zoom, the old symbols
    // fade out while the new ones fade in, so no label set pops between the
    // two states
    internal const int PlacementFadeMs = 300;

    // The placement the last frame drew its symbols from (reference only:
    // a reference change marks the switch between two symbol sets)
    private PlacementResult? _lastDrawnPlacement;
    private bool _lastDrawnWasHoldover;

    // The holdover that is currently fading out and when the fade started
    private PlacementResult? _fadingOutPlacement;
    private long _fadeStartTicks;

    // The crossfade interpolates over wall time; when the placement
    // finishes while the viewport stands still, nothing else invalidates
    // the map, so a ticker requests redraws for the duration of the fade
    private CancellationTokenSource? _fadeTickerCts;

    /// <summary>
    /// Computes the crossfade state of the symbol drawing for this frame:
    /// the holdover that still fades out (null when no fade is running),
    /// its remaining opacity and the growing opacity of the current
    /// placement. Must be called once per draw with the placement the frame
    /// will use; a reference change from the holdover of the previous zoom
    /// to the finished placement starts the fade.
    /// </summary>
    internal (PlacementResult? Old, float OldAlpha, float NewAlpha) GetPlacementFade(PlacementResult placement)
    {
        if (!ReferenceEquals(placement, _lastDrawnPlacement))
        {
            // Crossfade whenever the symbol set of another zoom replaces the
            // drawn set: a finished placement replaces the holdover of a zoom
            // change (the original case), and the cached result of a visited
            // zoom replaces the drawn set of the previous zoom immediately at
            // the zoom border, where there is no holdover phase in between —
            // without the fade the symbols of the new level pop in abruptly.
            // A new holdover and same-zoom replacements (the own placement of
            // the current zoom finished, the tile set changed on a pan) draw
            // instantly: the sets carry essentially the same labels
            if (_lastDrawnPlacement != null && !placement.IsHoldover &&
                (_lastDrawnWasHoldover || placement.ZoomLevel != _lastDrawnPlacement.ZoomLevel))
            {
                _fadingOutPlacement = _lastDrawnPlacement;
                _fadeStartTicks = Environment.TickCount64;
                StartPlacementFadeTicker();
            }
            else
            {
                StopPlacementFade();
            }

            _lastDrawnPlacement = placement;
            _lastDrawnWasHoldover = placement.IsHoldover;
        }

        if (_fadingOutPlacement == null)
        {
            return (null, 0f, 1f);
        }

        var progress = (Environment.TickCount64 - _fadeStartTicks) / (double)PlacementFadeMs;
        if (progress >= 1)
        {
            StopPlacementFade();
            return (null, 0f, 1f);
        }

        return (_fadingOutPlacement, 1f - (float)progress, (float)progress);
    }

    private void StartPlacementFadeTicker()
    {
        // Only the ticker of a previous fade is replaced here: the fade
        // state itself (the placement under fade-out) was just set by the
        // caller and must survive (StopPlacementFade would clear it)
        _fadeTickerCts?.Cancel();
        _fadeTickerCts?.Dispose();

        // The ticker requests redraws until the fade is over (the draw
        // that observes progress >= 1 stops it); one tick per third of the
        // fade duration keeps the animation smooth enough without flooding
        // the invalidation
        var ticker = new CancellationTokenSource();
        _fadeTickerCts = ticker;
        _ = Task.Run(async () =>
        {
            try
            {
                while (true)
                {
                    await Task.Delay(PlacementFadeMs / 3, ticker.Token).ConfigureAwait(false);
                    DataHasChanged();
                }
            }
            catch (OperationCanceledException)
            {
            }
        }, CancellationToken.None);
    }

    private void StopPlacementFade()
    {
        _fadingOutPlacement = null;
        _fadeTickerCts?.Cancel();
        _fadeTickerCts?.Dispose();
        _fadeTickerCts = null;
    }

    private void ResetPlacementFade()
    {
        _lastDrawnPlacement = null;
        _lastDrawnWasHoldover = false;
        StopPlacementFade();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RenderedTileLayer"/> class.
    /// </summary>
    /// <param name="tileSource">The rendered tile source to display.</param>
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
    /// <param name="fontNameResolver">
    /// Optional resolver that renames a font name of the style file (e.g.
    /// "noto_sans_regular" as used by glyph servers) to another font name
    /// (e.g. "Noto Sans Regular") before it is resolved. Returning null or
    /// an empty string leaves the name unchanged. The resolver is held
    /// globally by <see cref="FontCache"/>; the resolver of the last created
    /// layer wins.
    /// </param>
    public RenderedTileLayer(IRenderedTileSource tileSource, Func<string, string?>? fontSourceResolver = null,
        Func<string, string?>? fontNameResolver = null)
    {
        MapRenderer.RegisterLayerRenderer(_customLayerRendererName, RenderedTileLayerRenderer.Draw);

        TileSource = tileSource;

        // Set Attribution to most used Attribution of TileSource
        Attribution.Text = TileSource.Attribution.Text;
        Attribution.Url = TileSource.Attribution.Url;
        
        CustomLayerRendererName = _customLayerRendererName;
        _placementService = new PlacementService(tileSource);

        // The arrived tiles and the placement steps redraw the map without
        // the map having to move
        _placementService.TileArrived += _ => DataHasChanged();
        _placementService.PlacementCompleted += DataHasChanged;

        FontSourceResolver = fontSourceResolver;
        if (fontSourceResolver != null)
        {
            FontCache.SetFontSourceResolver(fontSourceResolver);
        }

        FontNameResolver = fontNameResolver;
        if (fontNameResolver != null)
        {
            FontCache.SetFontNameResolver(fontNameResolver);
        }

        _extent = TileSource.Schema.Extent.ToMRect();
    }

    /// <summary>
    /// Gets the tile source of this layer.
    /// </summary>
    public IRenderedTileSource TileSource { get; private set; }

    /// <summary>
    /// Gets the optional resolver that maps a font name to a source from
    /// which the font file is loaded when the font is missing. Null if no
    /// resolver was passed to the constructor.
    /// </summary>
    public Func<string, string?>? FontSourceResolver { get; }

    /// <summary>
    /// Gets the optional resolver that renames a font name of the style file
    /// before it is resolved. Null if no resolver was passed to the constructor.
    /// </summary>
    public Func<string, string?>? FontNameResolver { get; }

    /// <summary>
    /// Gets or sets a value indicating whether to show valid symbol borders for debugging.
    /// </summary>
    public bool ShowValidBorders { get; set; }

    /// <summary>
    /// Gets or sets the time in milliseconds the zoom must stay constant
    /// before the placement pass starts. While the zoom keeps changing every
    /// pass would be cancelled by the next change anyway.
    /// </summary>
    public int PlacementDebounceMs
    {
        get => _placementService.PlacementDebounceMs;
        set => _placementService.PlacementDebounceMs = value;
    }

    /// <summary>
    /// Gets or sets a value indicating whether the placement pass runs
    /// synchronously and blocking inside the update instead of in a
    /// background task. Used by tests and tooling.
    /// </summary>
    public bool SynchronousPlacement
    {
        get => _placementService.SynchronousPlacement;
        set => _placementService.SynchronousPlacement = value;
    }

    /// <inheritdoc />
    public override IReadOnlyList<double> Resolutions => TileSource.Schema.Resolutions.Select(r => r.Value.UnitsPerPixel).ToList();

    /// <inheritdoc />
    public override MRect? Extent => _extent;

    /// <summary>
    /// The zoom bounds of the schema: the resolution of the smallest and the
    /// resolution of the biggest zoom level. Set them on the navigator as
    /// override zoom bounds (Mapsui's default zoom bounds allow one zoom
    /// level beyond the biggest resolution of the layers), so the map cannot
    /// zoom below or above the zoom range of the schema:
    /// <c>mapControl.Map.Navigator.OverrideZoomBounds = layer.ZoomBounds;</c>
    /// </summary>
    public MMinMax ZoomBounds
    {
        get
        {
            var resolutions = TileSource.Schema.Resolutions;
            return new MMinMax(resolutions[resolutions.Keys.Min()].UnitsPerPixel,
                resolutions[resolutions.Keys.Max()].UnitsPerPixel);
        }
    }

    /// <summary>
    /// Clears the placement caches of the layer, so everything is recomputed
    /// on the next frame. Background tasks that are still running discard
    /// their results. (The cache of the tile source must be cleared
    /// separately: <c>tileSource.ClearCache()</c>, so the tiles are rendered
    /// again.)
    /// </summary>
    public void ClearCache()
    {
        _placementService.ClearCache();

        // The retained frames show the content of the old styles; drop them
        // so an empty view does not draw stale content
        var oldFrame = _retainedFrame;
        _retainedFrame = null;
        _retainedFrameKey = null;
        oldFrame?.Dispose();

        var oldGeometryFrame = _retainedGeometryFrame;
        _retainedGeometryFrame = null;
        _retainedGeometryFrameViewport = null;
        oldGeometryFrame?.Dispose();

        ResetPlacementFade();

        DataHasChanged();
    }

    /// <summary>
    /// Updates the placement service with the current view and returns the
    /// snapshot for the drawing phase: the arrived tiles and the placement
    /// result of the view (never null; until a new placement exists for the
    /// zoom, the retained set of the same zoom or the rescaled holdover of
    /// the previous zoom is returned, so the symbols stay on the screen
    /// until the placement of the new zoom replaces them).
    /// </summary>
    /// <param name="tileInfos">The tile infos of the current view.</param>
    /// <param name="zoomLevel">The canonical integer zoom level.</param>
    /// <param name="worldToScreen">
    /// Converts world coordinates into the unrotated canonical placement
    /// space the collision checks and placements are evaluated in.
    /// </param>
    /// <param name="centerWorld">
    /// The center of the viewport in world coordinates; tiles near the
    /// center are fetched first. Null keeps the order of the input.
    /// </param>
    /// <param name="screenTileInfos">
    /// The tile infos that really intersect the screen (the subset of
    /// <paramref name="tileInfos"/> visible on screen). Only a missing screen
    /// tile keeps the placement result incomplete; a missing tile of the
    /// padding ring (a data set that does not cover the whole ring, e.g. a
    /// local MBTiles file) does not stop the placement from being published.
    /// </param>
    public PlacementView Update(IReadOnlyList<TileInfo> tileInfos, int zoomLevel,
        Func<double, double, (double, double)> worldToScreen, (double X, double Y)? centerWorld = null,
        IReadOnlyList<TileInfo>? screenTileInfos = null)
    {
        var view = _placementService.Update(tileInfos, zoomLevel, worldToScreen, centerWorld, screenTileInfos);
        CurrentPlacement = view;
        return view;
    }

    /// <summary>
    /// The placement snapshot the last <see cref="Update"/> handed out (the
    /// arrived tiles of the view plus the placement result that the drawing
    /// phase uses). Sample/diagnostic surfaces can read it to show which
    /// symbols the placement has placed at the current zoom — for example to
    /// distinguish a placement that rejected a symbol from a drawing problem.
    /// </summary>
    public PlacementView? CurrentPlacement { get; private set; }

    /// <summary>
    /// Returns the placed point symbols (icon and/or text) whose envelope
    /// contains the given screen position, as feature objects ordered from
    /// the visually topmost symbol downwards. The test runs against the
    /// placement the last drawn frame drew its symbols from — during the
    /// short crossfade phase after a zoom change the fading-out holdover is
    /// not included, and line symbols (labels along lines) are not covered
    /// by the hit test. Returns an empty list when nothing was placed yet or
    /// nothing was hit.
    /// </summary>
    /// <param name="screenX">The x coordinate of the click position in screen pixels.</param>
    /// <param name="screenY">The y coordinate of the click position in screen pixels.</param>
    /// <param name="viewport">The current viewport of the map (including its rotation).</param>
    public IReadOnlyList<PlacedPointFeature> GetPointFeaturesAt(double screenX, double screenY, Viewport viewport)
    {
        return PlacedPointFeatureTester.GetPointFeaturesAt(_lastDrawnPlacement, screenX, screenY, viewport);
    }

    /// <inheritdoc />
    public Task<IDictionary<string, IEnumerable<IFeature>>> GetFeatureInfoAsync(Viewport viewport, ScreenPosition screenPosition)
    {
        IDictionary<string, IEnumerable<IFeature>> result = FeatureInfoResult(viewport, screenPosition.X, screenPosition.Y);
        return Task.FromResult(result);
    }

    /// <summary>
    /// The result of a feature info request at a screen position: the placed
    /// point symbols under the position, keyed by the layer name. A layer
    /// name that is null or empty uses the name of the type, so the result
    /// never carries a null key.
    /// </summary>
    private Dictionary<string, IEnumerable<IFeature>> FeatureInfoResult(Viewport viewport, double x, double y)
    {
        var key = string.IsNullOrEmpty(Name) ? GetType().Name : Name;
        return new Dictionary<string, IEnumerable<IFeature>> { [key] = GetPointFeaturesAt(x, y, viewport) };
    }

    /// <inheritdoc />
    public override IEnumerable<IFeature> GetFeatures(MRect box, double resolution)
    {
        return Array.Empty<IFeature>();
    }

    /// <summary>
    /// Compares the current viewport (including the device rect it maps to)
    /// with the one of the previous draw and stores it: an equal viewport in
    /// consecutive draws means the map stands still and only the content
    /// changed (e.g. the placement completed), so the frame can be drawn
    /// offscreen once and then blitted (fast and atomic). A changed viewport
    /// means a pan or zoom is running and the frame is drawn directly.
    /// Returns true when the viewport is unchanged.
    /// </summary>
    internal bool ViewportEqualsLastDraw(object viewportSignature)
    {
        var unchanged = Equals(_lastViewportSignature, viewportSignature);
        _lastViewportSignature = viewportSignature;
        return unchanged;
    }

    /// <summary>
    /// Returns the offscreen surface of the given size (in pixels) the layer
    /// content can be rasterized into; reuses the existing surface whenever
    /// the size has not changed.
    /// </summary>
    internal SKSurface GetFrameSurface(int width, int height, GRContext? grContext)
    {
        // The stored surface is invalid when the size changed, the GRContext
        // was replaced by the platform (e.g. the window moved to another
        // monitor) or the context was abandoned
        var contextInvalid = grContext != _frameGrContext ||
                             (grContext != null && (_frameGrContext == null || grContext.Handle != _frameGrContext.Handle));
        if (_frameSurface == null || _frameSurface.Canvas.DeviceClipBounds.Width != width ||
            _frameSurface.Canvas.DeviceClipBounds.Height != height || contextInvalid ||
            _frameGrContext is { IsAbandoned: true })
        {
            _frameSurface?.Dispose();
            _frameSurface = null;

            var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
            _frameSurface = grContext != null && !grContext.IsAbandoned
                ? SKSurface.Create(grContext, true, info)
                : SKSurface.Create(info);
            _frameGrContext = grContext;
        }

        return _frameSurface;
    }

    /// <summary>
    /// Draws the geometry-only frame of the last fully drawn state onto the
    /// canvas, mapped into the current viewport (used when the current view
    /// has no arrived tiles or while tiles are still missing, e.g. right
    /// after a zoom change, so the previous map content stays on the screen
    /// instead of a cleared map). The frame is drawn WITHOUT symbols: after
    /// a zoom change the labels of the old zoom are invalid, and inside a
    /// mapped image they would show scaled to the new resolution. Returns
    /// false when no frame is retained.
    /// </summary>
    internal bool DrawRetainedGeometryFrame(SKCanvas canvas, Viewport current)
    {
        if (_retainedGeometryFrame == null || _retainedGeometryFrameViewport is not { } old)
        {
            return false;
        }

        var matrix = CreateRetainedFrameMatrix(
            old, _retainedGeometryFrameRect, _retainedGeometryFrame.Width, _retainedGeometryFrame.Height,
            current, canvas.TotalMatrix);

        if (matrix == null)
        {
            return false;
        }

        canvas.Save();
        canvas.SetMatrix(matrix.Value);
        canvas.DrawImage(_retainedGeometryFrame, 0, 0);
        canvas.Restore();
        return true;
    }

    /// <summary>
    /// Builds the affine transform that maps an image of the given pixel
    /// size (drawn for the stored viewport, covering the stored device
    /// rect) so that its content is at the correct world position and in
    /// the correct scale of the current viewport. Internal for tests;
    /// returns null when the transform is degenerate.
    /// </summary>
    internal static SKMatrix? CreateRetainedFrameMatrix(
        (double CenterX, double CenterY, double Resolution, double Rotation,
            double Width, double Height) old, SKRect oldDestRect,
        float imageWidth, float imageHeight, Viewport current, SKMatrix canvasMatrix)
    {
        // The image covers the old viewport in DEVICE pixels: its pixel size
        // is the logical viewport size times the pixel density of the old
        // draw. The corners must be converted back to the old LOGICAL
        // pixels before they are mapped to world positions, otherwise the
        // frame is drawn with a wrong scale and offset (visible on HiDPI
        // screens).
        var densityX = oldDestRect.Width / (float)old.Width;
        var densityY = oldDestRect.Height / (float)old.Height;
        if (densityX <= 0 || densityY <= 0)
        {
            return null;
        }

        var oldViewport = new Viewport(old.CenterX, old.CenterY, old.Resolution, old.Rotation,
            old.Width, old.Height);

        var imageCornerToWorld = new MPoint[3];
        imageCornerToWorld[0] = oldViewport.ScreenToWorld(0, 0);
        imageCornerToWorld[1] = oldViewport.ScreenToWorld(imageWidth / densityX, 0);
        imageCornerToWorld[2] = oldViewport.ScreenToWorld(0, imageHeight / densityY);

        var imageToDevice = new SKPoint[3];
        for (var i = 0; i < 3; i++)
        {
            var logical = current.WorldToScreen(imageCornerToWorld[i].X, imageCornerToWorld[i].Y);
            imageToDevice[i] = canvasMatrix.MapPoint((float)logical.X, (float)logical.Y);
        }

        return CreateAffine(
            new SKPoint(0, 0), imageToDevice[0],
            new SKPoint(imageWidth, 0), imageToDevice[1],
            new SKPoint(0, imageHeight), imageToDevice[2]);
    }

    /// <summary>
    /// Solves the affine image -> device transform from three point pairs:
    /// x' = a*x + c*y + tx, y' = b*x + d*y + ty. Internal for tests.
    /// </summary>
    internal static SKMatrix CreateAffine(SKPoint s0, SKPoint d0, SKPoint s1, SKPoint d1, SKPoint s2, SKPoint d2)
    {
        var dsx = d1.X - d0.X;
        var dsy = d1.Y - d0.Y;
        var dtx = d2.X - d0.X;
        var dty = d2.Y - d0.Y;
        var ssx = s1.X - s0.X;
        var ssy = s1.Y - s0.Y;
        var stx = s2.X - s0.X;
        var sty = s2.Y - s0.Y;

        var det = ssx * sty - ssy * stx;
        if (Math.Abs(det) < 1e-12)
        {
            return SKMatrix.Identity;
        }

        var a = (dsx * sty - stx * dtx) / det;
        var c = (ssx * dtx - dsx * ssy) / det;
        var b = (dsy * sty - stx * dty) / det;
        var d = (ssx * dty - dsy * ssy) / det;

        return new SKMatrix
        {
            ScaleX = a,
            SkewY = b,
            SkewX = c,
            ScaleY = d,
            TransX = d0.X - a * s0.X - c * s0.Y,
            TransY = d0.Y - b * s0.X - d * s0.Y,
            Persp0 = 0,
            Persp1 = 0,
            Persp2 = 1,
        };
    }

    /// <summary>
    /// True when a retained frame of a previous complete draw exists.
    /// </summary>
    internal bool HasRetainedFrame => _retainedFrame != null;

    internal bool HasRetainedGeometryFrame => _retainedGeometryFrame != null;

    /// <summary>
    /// Draws the retained frame when the given key matches the key the
    /// current frame was drawn with (unchanged viewport and view). The
    /// expensive style layer rasterization is then skipped completely.
    /// </summary>
    internal bool TryReuseFrame(object key, SKCanvas canvas)
    {
        if (_retainedFrame == null || !Equals(_retainedFrameKey, key))
        {
            return false;
        }

        canvas.Save();
        canvas.ResetMatrix();
        canvas.DrawImage(_retainedFrame, new SKRect(0, 0, _retainedFrame.Width, _retainedFrame.Height),
            _retainedFrameRect);
        canvas.Restore();
        return true;
    }

    /// <summary>
    /// Stores the given snapshot of a fully drawn frame as the retained
    /// frame (with the dest rect and the frame key of the draw); disposes
    /// the previous one.
    /// </summary>
    internal void SetRetainedFrame(SKImage frame, SKRect destRect, object key)
    {
        var old = _retainedFrame;
        _retainedFrame = frame;
        _retainedFrameRect = destRect;
        _retainedFrameKey = key;
        old?.Dispose();
    }

    /// <summary>
    /// Stores the given snapshot of a geometry-only frame (same draw, but
    /// without the placed symbols) as the retained geometry frame; it is
    /// mapped into changed viewports while tiles are missing. Disposes the
    /// previous one.
    /// </summary>
    internal void SetRetainedGeometryFrame(SKImage frame, SKRect destRect, Viewport viewport)
    {
        var old = _retainedGeometryFrame;
        _retainedGeometryFrame = frame;
        _retainedGeometryFrameRect = destRect;
        _retainedGeometryFrameViewport = (viewport.CenterX, viewport.CenterY, viewport.Resolution,
            viewport.Rotation, viewport.Width, viewport.Height);
        old?.Dispose();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposed)
    {
        base.Dispose(disposed);

        if (disposed)
        {
            _frameSurface?.Dispose();
            _frameSurface = null;
            _frameGrContext = null;
            _retainedFrame?.Dispose();
            _retainedFrame = null;
            _retainedFrameKey = null;
            _retainedGeometryFrame?.Dispose();
            _retainedGeometryFrame = null;
            _retainedGeometryFrameViewport = null;
            _lastViewportSignature = null;
            ResetPlacementFade();
        }
    }
}
