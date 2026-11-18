using BruTile;
using Mapsui;
using Mapsui.Extensions;
using Mapsui.Layers;
using Mapsui.Rendering;
using NetTopologySuite.Index.Quadtree;
using SkiaSharp;
using VecTiles.Common.Enums;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Controls.Mapsui.Extensions;
using VecTiles.Renderers.Skia;

namespace VecTiles.Controls.Mapsui;

/// <summary>
/// Renders symbols for a <see cref="RenderedSymbolsLayer"/> using Skia.
/// </summary>
public static class RenderedSymbolsLayerRenderer
{
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

        // Use rotation divided by 5 to limit the number of cache keys
        var key = new TilesKey(tiles.ToTile(), (int)(viewport.Rotation / 5));

        // Snap the zoom level to the canonical integer level: computing the
        // zoom from the resolution is subject to rounding (a canonical
        // resolution can evaluate to 19 - 1e-14), which would push the whole
        // placement onto the next lower level. The snap epsilon is far below
        // any perceivable zoom difference (one zoom unit = resolution / 2).
        var zoomLevel = (int)Math.Floor(viewport.Resolution.ToZoomLevel() + 1e-6);
        var scale = 1f;
        var rotation = -viewport.Rotation;

        // Placement is a function of the integer zoom only: all measurements,
        // candidate positions and collision boxes are computed in the canonical
        // screen space of the integer zoom level, so labels keep their anchors
        // while zooming instead of running along their lines.
        var placementScale = (float)(zoomLevel.ToResolution() / viewport.Resolution);

        // Style sizes (text-size, spacing, ...) follow the fractional zoom of
        // the viewport, so icons and text keep the size the style gives them
        // at the current zoom like before; only the placement is frozen.
        var drawContext = new EvaluationContext((float)viewport.Resolution.ToZoomLevel(), 1f / scale, (float)viewport.Rotation);

        var context = new EvaluationContext(zoomLevel, 1f / scale, (float)viewport.Rotation);

        var symbols = renderedLayer.GetOrCreateSymbols(tiles, zoomLevel).ToArray();
        var tree = new Quadtree<ISymbol>();

        // Only the first occurrence of a symbol can be drawn, so track all
        // examined ids of this frame and discard following occurrences
        // before doing any work for them
        var examinedIds = new HashSet<ulong>();

        // Converts world coordinates into the canonical placement space
        Func<double, double, (double, double)> placementWorldToScreen =
            (x, y) =>
            {
                var p = viewport.WorldToScreen(x, y);
                return (p.X / placementScale, p.Y / placementScale);
            };

        // Converts world coordinates into the real screen space used for drawing
        Func<double, double, (double, double)> drawWorldToScreen =
            (x, y) =>
            {
                var p = viewport.WorldToScreen(x, y);
                return (p.X, p.Y);
            };

        for (var i = 0; i < symbols.Count(); i++)
        {
            var symbol = symbols[i];
            
            // Skip symbols with an id that was already examined in this frame
            if (!examinedIds.Add(symbol.Id))
            {
                continue;
            }

            canvas.Save();

            // Check if the symbol has a flag indicating it shouldn't be drawn for this tile range
            if (!symbol.DrawNotForTiles(key))
            {
                if (symbol is PointSymbol pointSymbol)
                {
                    var status = PointSymbolRenderer.CheckForSpace(canvas,
                        context, symbol, tree,
                        placementWorldToScreen,
                        rotation, renderedLayer.ShowInvalidBorders);

                    if (status == SymbolPlacementStatus.Success)
                    {
                        PointSymbolRenderer.Draw(canvas, context, pointSymbol,
                            ref tree, placementWorldToScreen, rotation,
                            renderedLayer.ShowValidBorders,
                            drawContext, drawWorldToScreen);
                    }
                    else if (status == SymbolPlacementStatus.Collision)
                    {
                        pointSymbol.Add(key);
                    }
                }

                if (symbol is LineSymbol lineSymbol)
                {
                    var status = LineSymbolRenderer.CheckForSpace(canvas,
                        context, symbol, tree,
                        placementWorldToScreen,
                        rotation, renderedLayer.ShowInvalidBorders);

                    if (status == SymbolPlacementStatus.Success)
                    {
                        LineSymbolRenderer.Draw(canvas, context, lineSymbol,
                            ref tree, placementWorldToScreen, rotation,
                            renderedLayer.ShowValidBorders,
                            drawContext, drawWorldToScreen);
                    }
                    else if (status == SymbolPlacementStatus.Collision)
                    {
                        symbol.Add(key);
                    }
                }
            }

            canvas.Restore();
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
