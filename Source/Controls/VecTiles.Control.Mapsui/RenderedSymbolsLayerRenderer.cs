using Mapsui;
using Mapsui.Extensions;
using Mapsui.Layers;
using Mapsui.Rendering;
using NetTopologySuite.Index.Quadtree;
using SkiaSharp;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Controls.Mapsui.Extensions;
using VecTiles.Renderers.Skia;

namespace VecTiles.Controls.Mapsui;

public static class RenderedSymbolsLayerRenderer
{
    public static void Draw(SKCanvas canvas, Viewport viewport, ILayer layer, RenderService renderService)
    {
        if (layer is not RenderedSymbolsLayer renderedLayer)
        {
            return;
        }

        var tileInfos = GetTilesOnScreen(viewport, renderedLayer.TileSource.Schema);

        if (tileInfos == null || !tileInfos.Any())
        {
            // No tiles on screen
            return;
        }

        var zoomLevel = (int)Math.Floor(viewport.Resolution.ToZoomLevel());
        var scale = 1f; // / canvas.TotalMatrix.ScaleX;
        var rotation = -viewport.Rotation; // * Math.PI / 180.0;

        var context = new EvaluationContext((float)viewport.Resolution.ToZoomLevel(), (float)(1f / scale), (float)viewport.Rotation);

        var symbols = renderedLayer.GetOrCreateSymbols(tileInfos, zoomLevel);
        var tree = new Quadtree<ISymbol>();

        foreach (var symbol in symbols)
        {
            canvas.Save();

            if (symbol is PointSymbol pointSymbol)
            {
                Func<double, double, (double, double)> worldToScreenConverter = (x, y) => { var p = viewport.WorldToScreen(x, y); return (p.X, p.Y); };

                if (PointSymbolRenderer.CheckForSpace(canvas, context, symbol, tree, worldToScreenConverter, rotation, renderedLayer.ShowInvalidBorders))
                {
                    PointSymbolRenderer.Draw(canvas, context, symbol, ref tree, worldToScreenConverter, rotation, renderedLayer.ShowValidBorders);
                }
            }

            if (symbol is LineSymbol lineSymbol)
            {
                Func<double, double, (double, double)> worldToScreenConverter = (x, y) => { var p = viewport.WorldToScreen(x, y); return (p.X, p.Y); };

                if (LineSymbolRenderer.CheckForSpace(canvas, context, symbol, tree, worldToScreenConverter, rotation, renderedLayer.ShowInvalidBorders))
                {
                    LineSymbolRenderer.Draw(canvas, context, symbol, ref tree, worldToScreenConverter, rotation, renderedLayer.ShowValidBorders);
                }
            }

            canvas.Restore();
        }
    }

    private static IEnumerable<BruTile.TileInfo> GetTilesOnScreen(Viewport viewport, BruTile.ITileSchema schema)
    {
        var point1 = viewport.ScreenToWorld(0, 0);
        var point2 = viewport.ScreenToWorld(viewport.Width, viewport.Height);

        var screenExtent = new BruTile.Extent(Math.Min(point1.X, point2.X), Math.Min(point1.Y, point2.Y), Math.Max(point1.X, point2.X), Math.Max(point1.Y, point2.Y));

        return schema.GetTileInfos(screenExtent, viewport.Resolution);
    }
}
