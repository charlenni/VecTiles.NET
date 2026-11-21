using BruTile;
using Mapsui;
using Mapsui.Extensions;
using Mapsui.Layers;
using Mapsui.Rendering;
using Mapsui.Tiling.Extensions;
using NetTopologySuite.Index.Quadtree;
using SkiaSharp;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Controls.Mapsui.Extensions;
using VecTiles.Renderers.Common;
using VecTiles.Renderers.Common.Interfaces;
using VecTiles.Renderers.Skia;
using VecTiles.Renderers.Skia.Primitives;

namespace VecTiles.Controls.Mapsui;

/// <summary>
/// Draws a <see cref="RenderedTileLayer"/>: style layer by style layer, in
/// the order the style file lists them — for every style layer at first its
/// geometry over all visible tiles, right afterwards its placed symbols (so
/// the background of one tile cannot overpaint the lines of the neighboring
/// tile, and a symbol of a style layer never lands on top of the geometry of
/// a later style layer). Layers with visibility none or outside
/// minzoom/maxzoom of the drawn zoom are skipped. The symbols are only drawn
/// when a placement result exists (the placement runs in the background and
/// triggers a redraw when its result is ready).
/// </summary>
public static class RenderedTileLayerRenderer
{
    private static readonly SKPaint DebugPaint = new() { Color = SKColors.Orange, StrokeWidth = 1, IsStroke = true };

    private static readonly SKRect TileRect = new(0, 0, 512, 512);

    /// <summary>
    /// Draws the layer onto the specified canvas.
    /// </summary>
    /// <param name="canvas">The Skia canvas to draw on.</param>
    /// <param name="viewport">The current viewport.</param>
    /// <param name="layer">The layer to render.</param>
    /// <param name="renderService">The render service (unused).</param>
    public static void Draw(SKCanvas canvas, Viewport viewport, ILayer layer, RenderService renderService)
    {
        if (layer is not RenderedTileLayer renderedLayer)
        {
            return;
        }

        var schema = renderedLayer.TileSource.Schema;

        // The screen extent of the tiles (the bounding box of the four
        // screen corners, so rotated viewports are covered) and the extent
        // for the placement: one tile ring around the screen, so that a
        // slight panning does not change the tile set of the placement and
        // most of the symbols stay where they are
        var screenExtent = GetExtentOfTilesOnScreen(viewport);
        var tileWorldSize = 512.0 * viewport.Resolution;
        var placementExtent = screenExtent.Grow(tileWorldSize);

        var screenTileInfos = schema.GetTileInfos(screenExtent, viewport.Resolution);

        if (!screenTileInfos.Any())
        {
            // No tiles are visible on screen
            return;
        }

        var placementTileInfos = schema.GetTileInfos(placementExtent, viewport.Resolution).ToArray();

        // Snap the zoom level to the canonical integer level: computing the
        // zoom from the resolution is subject to rounding (a canonical
        // resolution can evaluate to 19 - 1e-14), which would push the whole
        // placement onto the next lower level. The snap epsilon is far below
        // any perceivable zoom difference (one zoom unit = resolution / 2).
        var zoomLevel = (int)Math.Floor(viewport.Resolution.ToZoomLevel() + 1e-6);
        var rotation = -viewport.Rotation;

        // Placement is a function of the integer zoom only: all measurements,
        // candidate positions and collision boxes are computed in the canonical
        // screen space of the integer zoom level, so labels keep their anchors
        // while zooming instead of running along their lines.
        var placementScale = (float)(zoomLevel.ToResolution() / viewport.Resolution);

        // Style sizes (text-size, spacing, ...) follow the fractional zoom of
        // the viewport, so icons and text keep the size the style gives them
        // at the current zoom like before; only the placement is frozen.
        var drawContext = new EvaluationContext((float)viewport.Resolution.ToZoomLevel(), 1f, (float)viewport.Rotation);

        // The placement pass runs without rotation in the unrotated viewport
        // (rotation 0): map-oriented symbols only depend on their own
        // rotation, viewport-oriented symbols get rotation-safe envelopes.
        var context = new EvaluationContext(zoomLevel);

        // Unrotated projection of the current viewport: same center, size and
        // resolution, but rotation 0
        var center = viewport.ScreenToWorld(viewport.Width / 2, viewport.Height / 2);
        var placementViewport = new Viewport(center.X, center.Y, viewport.Resolution, 0, viewport.Width, viewport.Height);

        // Converts world coordinates into the unrotated canonical placement space
        Func<double, double, (double, double)> placementWorldToScreen =
            (x, y) =>
            {
                var p = placementViewport.WorldToScreen(x, y);
                return (p.X / placementScale, p.Y / placementScale);
            };

        // Converts world coordinates into the real screen space used for drawing
        Func<double, double, (double, double)> drawWorldToScreen =
            (x, y) =>
            {
                var p = viewport.WorldToScreen(x, y);
                return (p.X, p.Y);
            };

        // Updates the placement service with the current view and returns the
        // (possibly empty) placement result together with the arrived tiles:
        // the center of the viewport is the world center of the (unrotated)
        // viewport, so tiles near the center are fetched first
        var view = renderedLayer.Update(placementTileInfos, zoomLevel, placementWorldToScreen,
            (center.X, center.Y));

        // Draws the style layers in the order the style file lists them: for
        // every style layer at first its geometry over all tiles, afterwards
        // the placed symbols of the same style layer (the placement result
        // may be empty — then no symbols are drawn at all)
        DrawInStyleLayerOrder(canvas, viewport, view, screenExtent, rotation, renderedLayer,
            drawContext, placementWorldToScreen, drawWorldToScreen, placementScale, context);
    }

    /// <summary>
    /// Draws one single rendered tile (its geometry layers) for the given
    /// tile info, without symbols. Used by the layer drawing (per tile) and
    /// by tests and benchmarks that draw a tile directly.
    /// </summary>
    public static void DrawGeometryTile(SKCanvas canvas, Viewport viewport, TileInfo tileInfo, IRenderedTile renderedTile,
        EvaluationContext? context = null)
    {
        canvas.Save();

        var scale = CreateMatrix(canvas, viewport, ToMRect(tileInfo.Extent));
        var drawContext = new EvaluationContext(ResolutionToZoomLevel(viewport.Resolution), 1f / scale, (float)viewport.Rotation);

        canvas.ClipRect(TileRect);

        foreach (var entry in renderedTile.Layers)
        {
            if (entry is RenderedGeometryLayer geometryLayer)
            {
                geometryLayer.Renderer.Draw(canvas, context ?? drawContext);
            }
        }

        canvas.Restore();
    }

    /// <summary>
    /// Draws the arrived tiles in the order the style file lists the style
    /// layers: for every style layer at first its geometry is drawn over all
    /// visible tiles (so an entry of a later style layer never overpaints the
    /// entries of the earlier style layers of the neighboring tiles), right
    /// afterwards the placed symbols of the same style layer. The symbols are
    /// only drawn when the placement result contains entries for the layer
    /// (the placement runs in the background and triggers a redraw when its
    /// result is ready).
    /// </summary>
    private static void DrawInStyleLayerOrder(SKCanvas canvas, Viewport viewport, PlacementView view,
        BruTile.Extent screenExtent, double rotation, RenderedTileLayer renderedLayer,
        EvaluationContext drawContext,
        Func<double, double, (double, double)> placementWorldToScreen,
        Func<double, double, (double, double)> drawWorldToScreen, float placementScale,
        EvaluationContext context)
    {
        // The actually drawn zoom: the style file zoom entries (minzoom /
        // maxzoom) must be checked against this fractional zoom, not against
        // the tile zoom they were baked at (the tile of a fractional zoom is
        // the nearest neighbor level, which may lie one level above)
        var drawZoom = (float)viewport.Resolution.ToZoomLevel();

        // The tiles of the view that really intersect the screen: the
        // placement runs over the padded tile set, the drawing does not have
        // to draw the tiles of the padding ring (their geometry is off screen)
        // — the tiles are drawn in tile id order
        var tiles = view.Tiles
            .Where(t => t.Info.Extent.Intersects(screenExtent))
            .OrderBy(t => t.Tile.Tile.Id)
            .ToList();

        if (tiles.Count == 0)
        {
            return;
        }

        // The merged style layer order over all tiles of the view (including
        // the padded ring: a symbol-only layer present in a padding tile
        // still draws its labels at the screen edge). Not every tile carries
        // an entry for every style layer (styles without data produce no
        // entry), so the style file order is taken from the StyleIndex of the
        // entries, not from the first occurrence across the tiles.
        var layerIndex = new Dictionary<string, int>();
        foreach (var tileInView in view.Tiles)
        {
            foreach (var entry in tileInView.Tile.Layers)
            {
                if (!layerIndex.TryGetValue(entry.Name, out var index) || entry.StyleIndex < index)
                {
                    layerIndex[entry.Name] = entry.StyleIndex;
                }
            }
        }

        var names = layerIndex.OrderBy(pair => pair.Value).Select(pair => pair.Key).ToList();

        var tree = new Quadtree<ISymbol>();

        foreach (var name in names)
        {
            // The geometry of the style layer across all screen tiles
            foreach (var tileInView in tiles)
            {
                if (tileInView.Tile.Layers.FirstOrDefault(e => e is RenderedGeometryLayer && e.Name == name)
                    is not RenderedGeometryLayer geometryLayer)
                {
                    continue;
                }

                if (!SymbolPlacementEngine.IsVisibleAtZoom((geometryLayer.MinZoom, geometryLayer.MaxZoom), drawZoom))
                {
                    continue;
                }

                DrawGeometryLayer(canvas, viewport, tileInView.Info, geometryLayer);
            }

            // The placed symbols of the same style layer; the layer metadata
            // comes from the tiles the placement read its symbols from, so a
            // layer invisible at the drawn zoom draws no symbols even when
            // the placement placed them at the canonical zoom
            if (view.Placement.PlacedByLayer.TryGetValue(name, out var placed) == false || placed.Count == 0)
            {
                continue;
            }

            if (FindSymbolLayer(view, name) is { } symbolLayer &&
                !SymbolPlacementEngine.IsVisibleAtZoom((symbolLayer.MinZoom, symbolLayer.MaxZoom), drawZoom))
            {
                continue;
            }

            foreach (var entry in placed)
            {
                if (entry.Symbol is PointSymbol pointSymbol)
                {
                    PointSymbolRenderer.Draw(canvas, context, pointSymbol, ref tree,
                        placementWorldToScreen, rotation, renderedLayer.ShowValidBorders,
                        drawContext, drawWorldToScreen, checkForSpace: false);
                }
                else if (entry.Symbol is LineSymbol lineSymbol)
                {
                    LineSymbolRenderer.Draw(canvas, context, lineSymbol, ref tree,
                        placementWorldToScreen, rotation, renderedLayer.ShowValidBorders,
                        drawContext, drawWorldToScreen, entry.Run!);
                }
            }
        }

        if (renderedLayer.ShowInvalidBorders)
        {
            DrawInvalidBorders(canvas, view.Placement.Rejected, placementWorldToScreen, placementScale, drawWorldToScreen);
        }
    }

    /// <summary>
    /// The first symbol layer entry with the given name over all tiles of the
    /// view (all tiles carry the same minzoom/maxzoom for a style layer);
    /// null when no tile has such an entry (then the drawing applies no zoom
    /// filter of its own — the placement pass already filtered).
    /// </summary>
    private static RenderedSymbolLayer? FindSymbolLayer(PlacementView view, string name)
    {
        foreach (var tileInView in view.Tiles)
        {
            if (tileInView.Tile.Layers.FirstOrDefault(e => e is RenderedSymbolLayer && e.Name == name)
                is RenderedSymbolLayer symbolLayer)
            {
                return symbolLayer;
            }
        }

        return null;
    }

    /// <summary>
    /// Draws one single geometry layer of a rendered tile with the per-tile
    /// transform, without the other layers of the tile (the style layer by
    /// style layer pass across all tiles calls this per layer).
    /// </summary>
    private static void DrawGeometryLayer(SKCanvas canvas, Viewport viewport, TileInfo tileInfo,
        RenderedGeometryLayer geometryLayer)
    {
        canvas.Save();

        var scale = CreateMatrix(canvas, viewport, ToMRect(tileInfo.Extent));
        var drawContext = new EvaluationContext(ResolutionToZoomLevel(viewport.Resolution), 1f / scale, (float)viewport.Rotation);

        canvas.ClipRect(TileRect);
        geometryLayer.Renderer.Draw(canvas, drawContext);

        canvas.Restore();
    }

    /// <summary>
    /// Draws the rejected collision envelopes as debug borders. The envelopes
    /// are stored in the unrotated canonical placement space; they are moved
    /// into the real screen space relative to the symbol anchor and drawn at
    /// placement size. Under map rotation the border orientation differs from
    /// the checked envelope (approximation, debugging only).
    /// </summary>
    private static void DrawInvalidBorders(SKCanvas canvas, IReadOnlyList<ISymbol> rejected,
        Func<double, double, (double, double)> placementWorldToScreen, float placementScale,
        Func<double, double, (double, double)> drawWorldToScreen)
    {
        foreach (var symbol in rejected)
        {
            if (symbol is not PointSymbol pointSymbol)
            {
                // Line symbols record their collision boxes only inside the
                // quadtree of the placement pass; for the debug display the
                // geometry envelope marks the rejected area
                var geometryEnvelope = (symbol as LineSymbol)?.Geometry.EnvelopeInternal;
                if (geometryEnvelope is null)
                {
                    continue;
                }

                var centerX = (geometryEnvelope.MinX + geometryEnvelope.MaxX) / 2;
                var centerY = (geometryEnvelope.MinY + geometryEnvelope.MaxY) / 2;

                var (minGX, minGY) = placementWorldToScreen(geometryEnvelope.MinX, geometryEnvelope.MinY);
                var (maxGX, maxGY) = placementWorldToScreen(geometryEnvelope.MaxX, geometryEnvelope.MaxY);
                var (placementCenterX, placementCenterY) = placementWorldToScreen(centerX, centerY);
                var (anchorX, anchorY) = drawWorldToScreen(centerX, centerY);

                var offX = (minGX - placementCenterX) * placementScale;
                var offY = (minGY - placementCenterY) * placementScale;

                canvas.DrawRect(new SKRect((float)(anchorX + offX), (float)(anchorY + offY),
                    (float)(anchorX + offX + (maxGX - minGX) * placementScale),
                    (float)(anchorY + offY + (maxGY - minGY) * placementScale)), DebugPaint);
                continue;
            }

            foreach (var envelope in new[] { pointSymbol.IconSymbol?.ScreenEnvelope, pointSymbol.TextSymbol?.ScreenEnvelope })
            {
                if (envelope is null)
                {
                    continue;
                }

                // Anchor in the placement space, to position the envelope
                var (placementAnchorX, placementAnchorY) = placementWorldToScreen(pointSymbol.Point.X, pointSymbol.Point.Y);
                var (drawX, drawY) = drawWorldToScreen(pointSymbol.Point.X, pointSymbol.Point.Y);

                var offX = (envelope.MinX - placementAnchorX) * placementScale;
                var offY = (envelope.MinY - placementAnchorY) * placementScale;
                var width = (envelope.MaxX - envelope.MinX) * placementScale;
                var height = (envelope.MaxY - envelope.MinY) * placementScale;

                canvas.DrawRect(new SKRect((float)(drawX + offX), (float)(drawY + offY),
                    (float)(drawX + offX + width), (float)(drawY + offY + height)), DebugPaint);
            }
        }
    }

    /// <summary>
    /// The world extent of the tiles on screen: the bounding box of the four
    /// screen corners, so the tiles of a rotated viewport are covered too.
    /// </summary>
    private static BruTile.Extent GetExtentOfTilesOnScreen(Viewport viewport)
    {
        var point1 = viewport.ScreenToWorld(0, 0);
        var point2 = viewport.ScreenToWorld(viewport.Width, 0);
        var point3 = viewport.ScreenToWorld(0, viewport.Height);
        var point4 = viewport.ScreenToWorld(viewport.Width, viewport.Height);

        var minX = Math.Min(Math.Min(point1.X, point2.X), Math.Min(point3.X, point4.X));
        var minY = Math.Min(Math.Min(point1.Y, point2.Y), Math.Min(point3.Y, point4.Y));
        var maxX = Math.Max(Math.Max(point1.X, point2.X), Math.Max(point3.X, point4.X));
        var maxY = Math.Max(Math.Max(point1.Y, point2.Y), Math.Max(point3.Y, point4.Y));

        return new BruTile.Extent(minX, minY, maxX, maxY);
    }

    private static MRect ToMRect(BruTile.Extent extent)
    {
        var (top, bottom) = extent.MinY > extent.MaxY
            ? (extent.MinY, extent.MaxY)
            : (extent.MaxY, extent.MinY);

        return new MRect(extent.MinX, bottom, extent.MaxX, top);
    }

    /// <summary>
    /// Applies the per-tile transform to the canvas: the canvas draws the
    /// tile in its 512 x 512 raster space afterwards.
    /// </summary>
    private static float CreateMatrix(SKCanvas canvas, Viewport viewport, MRect extent)
    {
        float scale;

        if (viewport.IsRotated() && viewport.Rotation != 0.0)
        {
            var destinationTopLeft = viewport.WorldToScreen(extent.Left, extent.Top);
            var destinationTopRight = viewport.WorldToScreen(extent.Right, extent.Top);

            var dx = destinationTopRight.X - destinationTopLeft.X;
            var dy = destinationTopRight.Y - destinationTopLeft.Y;

            scale = (float)Math.Sqrt(dx * dx + dy * dy) / 512f;

            canvas.Translate((float)destinationTopLeft.X, (float)destinationTopLeft.Y);
            if (viewport.IsRotated())
                canvas.RotateDegrees((float)viewport.Rotation);
            canvas.Scale(scale);
        }
        else
        {
            var destination = viewport.WorldToScreen(extent);

            var scaleX = (float)(destination.Width / 512f);
            var scaleY = (float)(destination.Height / 512f);

            scale = (float)((scaleX + scaleY) / 2.0);

            canvas.Translate((float)destination.Left, (float)destination.Bottom);
            if (viewport.IsRotated())
                canvas.RotateDegrees((float)viewport.Rotation);
            canvas.Scale(scaleX, scaleY);
        }

        return scale;
    }

    private static float ResolutionToZoomLevel(double resolution)
    {
        double initialResolution = 78271.51696402031; // With 512px tiles
        return (float)Math.Log(initialResolution / resolution, 2);
    }
}

/// <summary>
/// Extension methods for <see cref="Extent"/> used by the tile layer renderer.
/// </summary>
internal static class ExtentExtensions
{
    /// <summary>
    /// Grows the extent on all sides by the given amount (world units).
    /// </summary>
    /// <param name="extent">The extent to grow.</param>
    /// <param name="amount">The amount to add on every side.</param>
    /// <returns>The grown extent.</returns>
    public static Extent Grow(this Extent extent, double amount)
    {
        return new Extent(extent.MinX - amount, extent.MinY - amount, extent.MaxX + amount, extent.MaxY + amount);
    }
}
