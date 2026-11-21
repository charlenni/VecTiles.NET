using BruTile;
using Mapsui;
using Mapsui.Extensions;
using Mapsui.Layers;
using Mapsui.Rendering;
using Mapsui.Tiling.Extensions;
using NetTopologySuite.Index.Quadtree;
using SkiaSharp;
using VecTiles.Common.Enums;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Controls.Mapsui.Extensions;
using VecTiles.Renderers.Skia;
using VecTiles.Renderers.Skia.Primitives;

namespace VecTiles.Controls.Mapsui;

/// <summary>
/// Renders symbols for a <see cref="RenderedSymbolsLayer"/> using Skia.
/// </summary>
public static class RenderedSymbolsLayerRenderer
{
    private static readonly SKPaint DebugPaint = new() { Color = SKColors.Orange, StrokeWidth = 1, IsStroke = true };

    /// <summary>
    /// Draws all symbols from the rendered symbols layer onto the specified canvas.
    /// </summary>
    /// <param name="canvas">The Skia canvas to draw on.</param>
    /// <param name="viewport">The current viewport.</param>
    /// <param name="layer">The layer to render.</param>
    /// <param name="renderService">The render service (unused).</param>
    public static void Draw(SKCanvas canvas, Viewport viewport, ILayer layer, RenderService renderService)
    {
        if (layer is not RenderedSymbolsLayer renderedLayer)
        {
            return;
        }

        var tileInfos = GetTilesOnScreen(viewport, renderedLayer.TileSource.Schema);

        var tiles = tileInfos as TileInfo[] ?? [.. tileInfos];
        if (tiles.Length == 0)
        {
            // No tiles are visible on screen
            return;
        }

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

        // May be null while the placement runs in the background; the layer
        // fires DataChanged once the placement finished, so the next frame
        // draws the symbols. The center above is the world center of the
        // (unrotated) viewport: tiles near the center are fetched first
        var result = renderedLayer.GetPlacementResult(tiles, zoomLevel, placementWorldToScreen, center);
        if (result is null)
        {
            return;
        }

        // The collision checks and tree inserts already happened in the
        // placement pass; drawing consumes the decisions only
        var tree = new Quadtree<ISymbol>();

        // Layers in dictionary order = ascending style order: draw the layers
        // like the style file orders them
        foreach (var layerPair in result.PlacedByLayer)
        {
            foreach (var entry in layerPair.Value)
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
                        drawContext, drawWorldToScreen, entry.Run);
                }
            }
        }

        if (renderedLayer.ShowInvalidBorders)
        {
            DrawInvalidBorders(canvas, result.Rejected, placementWorldToScreen, placementScale, drawWorldToScreen);
        }
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
    /// Gets the tile information for tiles that are currently visible on screen.
    /// </summary>
    /// <param name="viewport">The current viewport.</param>
    /// <param name="schema">The tile schema of the tile source.</param>
    /// <returns>An enumerable of tile information for visible tiles.</returns>
    private static IEnumerable<TileInfo> GetTilesOnScreen(Viewport viewport, ITileSchema schema)
    {
        // Get all four corners of the screen in world coordinates
        var point1 = viewport.ScreenToWorld(0, 0);
        var point2 = viewport.ScreenToWorld(viewport.Width, 0);
        var point3 = viewport.ScreenToWorld(0, viewport.Height);
        var point4 = viewport.ScreenToWorld(viewport.Width, viewport.Height);

        // Calculate the bounding box from all four points to handle rotation
        var minX = Math.Min(Math.Min(point1.X, point2.X), Math.Min(point3.X, point4.X));
        var minY = Math.Min(Math.Min(point1.Y, point2.Y), Math.Min(point3.Y, point4.Y));
        var maxX = Math.Max(Math.Max(point1.X, point2.X), Math.Max(point3.X, point4.X));
        var maxY = Math.Max(Math.Max(point1.Y, point2.Y), Math.Max(point3.Y, point4.Y));

        var screenExtent = new Extent(minX, minY, maxX, maxY);

        return schema.GetTileInfos(screenExtent, viewport.Resolution);
    }
}
