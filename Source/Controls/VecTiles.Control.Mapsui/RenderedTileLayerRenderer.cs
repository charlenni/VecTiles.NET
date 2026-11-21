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
            // No tiles are visible on screen: keep the last fully drawn frame
            // on the screen (not the cleared canvas), e.g. while zooming out
            // and the bigger tiles of the new view have not arrived yet
            renderedLayer.DrawRetainedGeometryFrame(canvas, viewport);
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

        // Symbol crossfade: when the holdover of the previous zoom was
        // replaced by the finished placement of this zoom, the old symbols
        // fade out while the new ones fade in (frame key and drawing below
        // depend on the fade state, so every fade frame rasterizes fresh)
        var placementFade = renderedLayer.GetPlacementFade(view.Placement);

        // Mapsui scales the screen canvas by the pixel density (logical px *
        // density = device px). The offscreen frame must be rasterized in
        // device pixels at the same scale, so the blit is 1:1 in device
        // pixels and the content is not resampled (resampling would blur it)
        var matrix = canvas.TotalMatrix;
        var densityX = matrix.ScaleX;
        var densityY = matrix.ScaleY;
        if (densityX <= 0 || densityY <= 0 || float.IsNaN(densityX) || float.IsNaN(densityY))
        {
            densityX = 1;
            densityY = 1;
        }

        // The device space rect the viewport maps to: the blit target of the
        // offscreen frame (the canvas matrix is reset for the blit, so it is
        // not applied twice)
        var topLeft = matrix.MapPoint(0, 0);
        var bottomRight = matrix.MapPoint((float)viewport.Width, (float)viewport.Height);
        var destRect = new SKRect(topLeft.X, topLeft.Y, bottomRight.X, bottomRight.Y);

        // Fast path for unchanged frames: when the viewport, the tile set and
        // the placement result are the same as in the last drawn frame (e.g.
        // another layer has triggered a redraw, or the widgets draw again),
        // the retained frame is blitted instead of rasterizing the whole
        // style layer drawing again (this also makes the frame stable while
        // the placement runs). Update always returns a new PlacementView, so
        // the key is built from its content: the placement result reference
        // (the placement service hands out the same instance while nothing
        // changed) and a hash over the tile ids of the view.
        ulong tilesKey = 14695981039346656037;
        foreach (var tileInView in view.Tiles)
        {
            tilesKey = (tilesKey ^ tileInView.Tile.Tile.Id) * 1099511628211;
        }

        object frameKey = (view.Placement, tilesKey, viewport.CenterX, viewport.CenterY,
            viewport.Resolution, viewport.Rotation, viewport.Width, viewport.Height,
            destRect, zoomLevel, placementFade.Old, (byte)Math.Round(placementFade.OldAlpha * 255),
            // The border flags change the content of the frame: without them
            // a toggled checkbox would still blit the rasterized old frame
            renderedLayer.ShowValidBorders);
        if (renderedLayer.TryReuseFrame(frameKey, canvas))
        {
            return;
        }

        // The view may have no tile at all (the placement extent contains
        // tile infos, but none of them has arrived yet): keep the last
        // fully drawn geometry on the screen and draw the placed symbols on
        // top of it, so the labels do not disappear at the start of a zoom
        // change
        if (!view.Tiles.Any(t => t.Info.Extent.Intersects(screenExtent)))
        {
            renderedLayer.DrawRetainedGeometryFrame(canvas, viewport);
            DrawInStyleLayerOrder(canvas, viewport, view, screenExtent, rotation, renderedLayer,
                drawContext, placementWorldToScreen, drawWorldToScreen, placementScale, context,
                fadeOutPlacement: placementFade.Old, fadeOutAlpha: placementFade.OldAlpha,
                fadeInAlpha: placementFade.NewAlpha);
            return;
        }

        // A pan or zoom is running (the viewport changed since the previous
        // draw): draw directly onto the screen canvas, which is fast. One
        // Skia paint is presented atomically, so no intermediate state of
        // this single draw is visible — and the CPU-rasterized offscreen
        // frame of an interactive draw would fall far behind the frame rate
        // of the direct draw. A stable viewport with new content (the
        // placement completed, a tile arrived, only the widgets repainted)
        // is rasterized offscreen instead: the map content switch happens in
        // one atomic blit and the frame is retained afterwards.
        if (renderedLayer.ViewportEqualsLastDraw((viewport.CenterX, viewport.CenterY,
                viewport.Resolution, viewport.Rotation, viewport.Width, viewport.Height, destRect)))
        {
            var width = (int)Math.Round(viewport.Width * densityX);
            var height = (int)Math.Round(viewport.Height * densityY);
            if (width > 0 && height > 0)
            {
                // When the screen canvas is GPU backed, create the offscreen
                // surface in the same GRContext: the drawing and the blit
                // then run on the GPU (the raster fallback keeps tests and
                // non-GPU canvases working)
                var grContext = canvas.Surface?.Context as GRContext;
                var surface = renderedLayer.GetFrameSurface(width, height, grContext);
                surface.Canvas.Clear(SKColors.Transparent);
                surface.Canvas.Save();
                surface.Canvas.Scale(densityX, densityY);
                DrawRetainedFrameUnderlay(renderedLayer, surface.Canvas, viewport, view.Placement.Incomplete);
                DrawInStyleLayerOrder(surface.Canvas, viewport, view, screenExtent, rotation, renderedLayer,
                    drawContext, placementWorldToScreen, drawWorldToScreen, placementScale, context,
                    fadeOutPlacement: placementFade.Old, fadeOutAlpha: placementFade.OldAlpha,
                    fadeInAlpha: placementFade.NewAlpha);
                surface.Canvas.Restore();

                var frame = surface.Snapshot();
                canvas.Save();
                canvas.ResetMatrix();
                canvas.DrawImage(frame, new SKRect(0, 0, frame.Width, frame.Height), destRect);
                canvas.Restore();
                renderedLayer.SetRetainedFrame(frame, destRect, frameKey);

                // Second raster pass without symbols: the geometry-only
                // frame is what later fills changed viewports while tiles
                // are missing. Not refreshed while the crossfade runs: the
                // geometry does not depend on the fade state and the frame
                // of the last stable state is still valid
                if (placementFade.Old == null)
                {
                    surface.Canvas.Clear(SKColors.Transparent);
                    surface.Canvas.Save();
                    surface.Canvas.Scale(densityX, densityY);
                    DrawRetainedFrameUnderlay(renderedLayer, surface.Canvas, viewport, view.Placement.Incomplete);
                    DrawInStyleLayerOrder(surface.Canvas, viewport, view, screenExtent, rotation, renderedLayer,
                        drawContext, placementWorldToScreen, drawWorldToScreen, placementScale, context,
                        drawSymbols: false);
                    surface.Canvas.Restore();

                    var geometryFrame = surface.Snapshot();
                    renderedLayer.SetRetainedGeometryFrame(geometryFrame, destRect, viewport);
                }
            }

            return;
        }

        // While the tile set of the view is incomplete (e.g. the tiles of a
        // new zoom level are still arriving), the retained frame of the
        // previous view is drawn first, mapped into the current viewport:
        // the not yet arrived tile areas show the old map content scaled to
        // the new view (pixelated, but the image never falls back to a
        // cleared background and does not flicker)
        DrawRetainedFrameUnderlay(renderedLayer, canvas, viewport, view.Placement.Incomplete);

        DrawInStyleLayerOrder(canvas, viewport, view, screenExtent, rotation, renderedLayer,
            drawContext, placementWorldToScreen, drawWorldToScreen, placementScale, context,
            fadeOutPlacement: placementFade.Old, fadeOutAlpha: placementFade.OldAlpha,
            fadeInAlpha: placementFade.NewAlpha);
    }

    /// <summary>
    /// Draws the geometry-only frame of the previous view as the underlay of
    /// the current draw, but only while the placement of the current view is
    /// still incomplete (tiles missing); with a complete view the tiles
    /// cover the whole screen and the underlay would only be overdrawn.
    /// No symbols: the labels of the old zoom are invalid after a zoom
    /// change and would show scaled inside the mapped image.
    /// </summary>
    private static void DrawRetainedFrameUnderlay(RenderedTileLayer renderedLayer, SKCanvas canvas,
        Viewport viewport, bool incomplete)
    {
        if (incomplete && renderedLayer.HasRetainedGeometryFrame)
        {
            renderedLayer.DrawRetainedGeometryFrame(canvas, viewport);
        }
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
        EvaluationContext context, bool drawSymbols = true,
        PlacementResult? fadeOutPlacement = null, float fadeOutAlpha = 0f, float fadeInAlpha = 1f)
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

        // With no intersecting tile there is no geometry to draw, but the
        // geometry-only pass still stops here. The symbol pass continues:
        // at the start of a zoom change no tile of the new zoom has arrived
        // yet and the labels of the holdover/fade keep the map readable
        // while the mapped geometry-only frame fills the background
        if (tiles.Count == 0 && !drawSymbols)
        {
            return;
        }

        // The merged style layer order over all tiles of the view (including
        // the padded ring: a symbol-only layer present in a padding tile
        // still draws its labels at the screen edge). Not every tile carries
        // an entry for every style layer (styles without data produce no
        // entry), so the style file order is taken from the StyleIndex of the
        // entries, not from the first occurrence across the tiles. A style
        // layer is either a geometry or a symbol layer; the kind is recorded
        // alongside, so the drawing skips the pass that has nothing to draw.
        var layerIndex = new Dictionary<string, (int StyleIndex, bool IsGeometry)>();
        foreach (var tileInView in view.Tiles)
        {
            foreach (var entry in tileInView.Tile.Layers)
            {
                var isGeometry = entry is RenderedGeometryLayer;
                if (layerIndex.TryGetValue(entry.Name, out var existing))
                {
                    if (entry.StyleIndex < existing.StyleIndex)
                    {
                        layerIndex[entry.Name] = (entry.StyleIndex, isGeometry || existing.IsGeometry);
                    }
                }
                else
                {
                    layerIndex[entry.Name] = (entry.StyleIndex, isGeometry);
                }
            }
        }

        var names = layerIndex.OrderBy(pair => pair.Value.StyleIndex).Select(pair => pair.Key).ToList();

        // Symbol-only style layers that no arrived tile carries: at the very
        // start of a zoom change no tile of the new zoom has arrived yet, so
        // the layer index is empty and the names would only come from the
        // (holdover) placement. Its dictionary preserves the ascending style
        // order, so the missing keys are appended in that order
        if (drawSymbols)
        {
            foreach (var extra in view.Placement.PlacedByLayer.Keys)
            {
                if (!layerIndex.ContainsKey(extra))
                {
                    names.Add(extra);
                }
            }

            if (fadeOutPlacement != null)
            {
                foreach (var extra in fadeOutPlacement.PlacedByLayer.Keys)
                {
                    if (!layerIndex.ContainsKey(extra) && !names.Contains(extra))
                    {
                        names.Add(extra);
                    }
                }
            }
        }

        var tree = new Quadtree<ISymbol>();

        // The layout of the symbols of a placement result was measured in the
        // canonical space of the zoom the result was computed for; normally
        // that is the integer zoom of the view (the passed context), but the
        // holdover of a zoom change was measured in the space of its source
        // zoom, and drawing it with the view context would scale the glyphs
        // by the wrong ratio (one step too small)
        var newLayoutContext = LayoutContextOf(view.Placement, context);
        var oldLayoutContext = fadeOutPlacement is null ? null : LayoutContextOf(fadeOutPlacement, context);

        foreach (var name in names)
        {
            if (layerIndex.TryGetValue(name, out var layerMeta) && layerMeta.IsGeometry)
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

                continue;
            }

            // The placed symbols of the same style layer; the layer metadata
            // comes from the tiles the placement read its symbols from, so a
            // layer invisible at the drawn zoom draws no symbols even when
            // the placement placed them at the canonical zoom.
            // Skipped entirely for the geometry-only frame: symbols of an
            // old zoom must never end up inside a frame that is later mapped
            // into a changed viewport (there they would show scaled).
            // While the crossfade runs, the fading-out holdover draws its
            // entries of the same style layer before the current placement,
            // both wrapped in alpha layers
            if (!drawSymbols)
            {
                continue;
            }

            var newPlaced = view.Placement.PlacedByLayer.TryGetValue(name, out var list) &&
                list.Count > 0 ? list : null;
            var oldPlaced = fadeOutPlacement is not null && fadeOutAlpha > 0 &&
                fadeOutPlacement.PlacedByLayer.TryGetValue(name, out var oldList) &&
                oldList.Count > 0 ? oldList : null;

            if (newPlaced == null && oldPlaced == null)
            {
                continue;
            }

            if (FindSymbolLayer(view, name) is { } symbolLayer &&
                !SymbolPlacementEngine.IsVisibleAtZoom((symbolLayer.MinZoom, symbolLayer.MaxZoom), drawZoom))
            {
                continue;
            }

            if (oldPlaced != null)
            {
                using var oldLayerPaint = new SKPaint
                {
                    Color = SKColors.White.WithAlpha((byte)Math.Round(255 * fadeOutAlpha)),
                };
                canvas.SaveLayer(oldLayerPaint);
                foreach (var entry in oldPlaced)
                {
                    DrawPlacementEntry(canvas, viewport, rotation, renderedLayer, drawContext, oldLayoutContext,
                        placementWorldToScreen, drawWorldToScreen, entry, ref tree);
                }

                canvas.Restore();
            }

            if (newPlaced != null && fadeInAlpha > 0)
            {
                SKPaint? newLayerPaint = fadeInAlpha < 1
                    ? new SKPaint { Color = SKColors.White.WithAlpha((byte)Math.Round(255 * fadeInAlpha)) }
                    : null;
                if (newLayerPaint != null)
                {
                    canvas.SaveLayer(newLayerPaint);
                }

                foreach (var entry in newPlaced)
                {
                    DrawPlacementEntry(canvas, viewport, rotation, renderedLayer, drawContext, newLayoutContext,
                        placementWorldToScreen, drawWorldToScreen, entry, ref tree);
                }

                if (newLayerPaint != null)
                {
                    canvas.Restore();
                    newLayerPaint.Dispose();
                }
            }
        }
    }

    /// <summary>
    /// The evaluation context the layout measurements of the symbols of a
    /// placement result are read with (the font size ratio towards the
    /// drawn font size): normally the integer zoom of the view — the layout
    /// was measured in the canonical space of that zoom — but the holdover
    /// of a zoom change was measured in the canonical space of its source
    /// zoom, so it scales by the ratio towards that zoom (with the view
    /// context the glyphs would render at the layout size: one zoom step
    /// too small).
    /// </summary>
    private static EvaluationContext LayoutContextOf(PlacementResult placement, EvaluationContext viewContext)
    {
        if (placement.IsHoldover && placement.SourceZoomLevel > 0 &&
            placement.SourceZoomLevel != placement.ZoomLevel)
        {
            return new EvaluationContext(placement.SourceZoomLevel);
        }

        return viewContext;
    }

    /// <summary>
    /// Draws one placed entry (point or line symbol) of a placement result
    /// with the drawing settings of the current view; runs no collision
    /// checks and inserts nothing into the tree. The layout context is the
    /// evaluation context of the zoom the layout of the placement result was
    /// measured in (<see cref="LayoutContextOf"/>).
    /// </summary>
    private static void DrawPlacementEntry(SKCanvas canvas, Viewport viewport, double rotation,
        RenderedTileLayer renderedLayer, EvaluationContext drawContext, EvaluationContext context,
        Func<double, double, (double, double)> placementWorldToScreen,
        Func<double, double, (double, double)> drawWorldToScreen, PlacementEntry entry, ref Quadtree<ISymbol> tree)
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
