using BruTile;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Rendering;
using Mapsui.Rendering.Skia;
using Mapsui.Extensions;
using Mapsui.Tiling.Extensions;
using SkiaSharp;
using System.Linq;
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
public class RenderedTileLayer : BaseLayer
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

    // The last completely drawn frame; drawn when the view currently has no
    // tiles (e.g. zooming out before the new bigger tiles have arrived), so
    // the previous map content stays on the screen instead of a black screen
    private SKImage? _retainedFrame;
    private SKRect _retainedFrameRect;
    private object? _retainedFrameKey;

    // The viewport the retained frame was drawn for; the frame is mapped
    // from this viewport into the current viewport when it is drawn (so
    // after a zoom the retained frame shows the old content in the new
    // scale instead of frozen pixels of the old scale)
    private (double CenterX, double CenterY, double Resolution, double Rotation,
        double Width, double Height)? _retainedFrameViewport;

    // The viewport of the previous draw; equal viewports in consecutive draws
    // mean the map stands still (only the content changed, e.g. the
    // placement completed), so those frames are rasterized offscreen and
    // blitted atomically instead of drawn directly
    private object? _lastViewportSignature;

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
    public RenderedTileLayer(IRenderedTileSource tileSource, Func<string, string?>? fontSourceResolver = null)
    {
        MapRenderer.RegisterLayerRenderer(_customLayerRendererName, RenderedTileLayerRenderer.Draw);

        TileSource = tileSource;
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
    /// Gets or sets a value indicating whether to show valid symbol borders for debugging.
    /// </summary>
    public bool ShowValidBorders { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to show invalid symbol borders for debugging.
    /// </summary>
    public bool ShowInvalidBorders { get; set; }

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

        // The retained frame shows the content of the old styles; drop it so
        // an empty view does not draw stale content
        var oldFrame = _retainedFrame;
        _retainedFrame = null;
        _retainedFrameKey = null;
        _retainedFrameViewport = null;
        oldFrame?.Dispose();

        DataHasChanged();
    }

    /// <summary>
    /// Updates the placement service with the current view and returns the
    /// snapshot for the drawing phase: the arrived tiles and the placement
    /// result of the view (never null; until a new placement exists for the
    /// zoom, the retained set of the same zoom or an empty result, so no
    /// symbols are drawn after a zoom change).
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
    public PlacementView Update(IReadOnlyList<TileInfo> tileInfos, int zoomLevel,
        Func<double, double, (double, double)> worldToScreen, (double X, double Y)? centerWorld = null)
    {
        return _placementService.Update(tileInfos, zoomLevel, worldToScreen, centerWorld);
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
    /// Draws the retained frame of the last fully drawn state onto the
    /// canvas (used when the current view has no tiles at all, e.g. right
    /// after zooming out, so the previous map content stays on the screen
    /// instead of a cleared map). Returns false when no frame is retained.
    /// </summary>
    internal bool DrawRetainedFrame(SKCanvas canvas, Viewport current)
    {
        if (_retainedFrame == null || _retainedFrameViewport is not { } old)
        {
            return false;
        }

        var matrix = CreateRetainedFrameMatrix(
            old, _retainedFrameRect, _retainedFrame.Width, _retainedFrame.Height,
            current, canvas.TotalMatrix);

        if (matrix == null)
        {
            return false;
        }

        canvas.Save();
        canvas.SetMatrix(matrix.Value);
        canvas.DrawImage(_retainedFrame, 0, 0);
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
    internal void SetRetainedFrame(SKImage frame, SKRect destRect, object key, Viewport viewport)
    {
        var old = _retainedFrame;
        _retainedFrame = frame;
        _retainedFrameRect = destRect;
        _retainedFrameKey = key;
        _retainedFrameViewport = (viewport.CenterX, viewport.CenterY, viewport.Resolution,
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
            _retainedFrameViewport = null;
            _lastViewportSignature = null;
        }
    }
}
