using BruTile;
using SkiaSharp;
using VecTiles.Common.Interfaces;
using VecTiles.Converters.Mapsforge;
using VecTiles.Controls.Mapsui;
using Viewport = Mapsui.Viewport;

namespace VecTiles.Renderers.Skia.Test;

/// <summary>
/// Regression test for the stroke width under a pixel density: the screen
/// canvas of Mapsui on a HiDPI display carries the density in its total
/// matrix and the stable frame path rasterizes offscreen at that density.
/// The stroke must keep its width RELATIVE TO THE MAP GEOMETRY (the
/// density scales geometry and stroke together) -- otherwise strokes draw
/// twice as wide as in the original Mapsforge renderer.
/// </summary>
public class MFStrokeDensityFrameTests
{
    private static readonly string _mapFile = Path.Combine(Utilities.DataFiles, "..",
        "Mapsforge", "zuerich.map");
    private static readonly string _mapsforgeTheme = Path.Combine(Utilities.DataFiles, "..",
        "Mapsforge", "mapsforge", "default.xml");

    [Fact]
    public void StrokeKeepsRelativeWidthAtDensity2()
    {
        const int zoom = 16;
        const float density = 2f;
        const int logicalSize = 3 * 256;
        const int TileX = 34323; // Zuerich, Buerkliplatz / Quaibruecke
        const int TileY = 22951;

        // Canonical resolution of a 512 px tile (EPSG:3857)
        double resolution = 78271.51696402031 / Math.Pow(2, zoom);
        double tileSpan = 512.0 * resolution;

        double centerXWorld = -20037508.343 + (TileX + 0.5) * tileSpan;
        double centerYWorld = 20037508.343 - (TileY + 0.5) * tileSpan;
        var viewport = new Viewport(centerXWorld, centerYWorld, resolution, 0, logicalSize, logicalSize);

        using var tileSource = new MFRenderedTileSource(_mapFile, new IdentityTagsConverter("mapsforge"), _mapsforgeTheme);
        var tileLayer = new RenderedTileLayer(tileSource) { SynchronousPlacement = true };
        var renderService = new Mapsui.Rendering.RenderService();

        // Device pixel canvas with the density in the total matrix (the
        // stable frame path reads densityX/Y from canvas.TotalMatrix).
        // Two draws of the same viewport: the first is the direct draw, the
        // second the retained offscreen frame path at the density
        const int deviceSize = (int)(logicalSize * density);
        using var bitmap = new SKBitmap(deviceSize, deviceSize);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        canvas.Scale(density, density);
        RenderedTileLayerRenderer.Draw(canvas, viewport, tileLayer, renderService);
        RenderedTileLayerRenderer.Draw(canvas, viewport, tileLayer, renderService);

        // The strokes must scale with the map: the yellow share of the
        // device bitmap stays in the range of the density 1 rendering
        // (both bitmaps hold the same content at 2x device pixels, so the
        // share is about the same; a stroke drawn density times too wide
        // would roughly double it)
        var density1Share = YellowShare(RenderAtDensity(1f));
        var density2Share = YellowShare(bitmap);
        var ratio = density2Share / density1Share;

        Assert.InRange(ratio, 0.8, 1.25);

        static SKBitmap RenderAtDensity(float density)
        {
            const int zoom = 16;
            const int logicalSize = 3 * 256;
            const int TileX = 34323;
            const int TileY = 22951;

            double resolution = 78271.51696402031 / Math.Pow(2, zoom);
            double tileSpan = 512.0 * resolution;
            double centerXWorld = -20037508.343 + (TileX + 0.5) * tileSpan;
            double centerYWorld = 20037508.343 - (TileY + 0.5) * tileSpan;
            var viewport = new Viewport(centerXWorld, centerYWorld, resolution, 0, logicalSize, logicalSize);

            using var tileSource = new MFRenderedTileSource(_mapFile, new IdentityTagsConverter("mapsforge"), _mapsforgeTheme);
            var tileLayer = new RenderedTileLayer(tileSource) { SynchronousPlacement = true };
            var renderService = new Mapsui.Rendering.RenderService();

            var deviceSize = (int)(logicalSize * density);
            var bitmap = new SKBitmap(deviceSize, deviceSize);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.White);
            if (density != 1f)
            {
                canvas.Scale(density, density);
            }
            RenderedTileLayerRenderer.Draw(canvas, viewport, tileLayer, renderService);
            RenderedTileLayerRenderer.Draw(canvas, viewport, tileLayer, renderService);
            return bitmap;
        }

        static double YellowShare(SKBitmap bitmap)
        {
            var yellow = 0;
            for (int y = 0; y < bitmap.Height; y += 2)
            {
                for (int x = 0; x < bitmap.Width; x += 2)
                {
                    var c = bitmap.GetPixel(x, y);
                    if (c.Red > 230 && c.Green > 190 && c.Green < 230 && c.Blue < 90)
                    {
                        yellow++;
                    }
                }
            }
            var total = (bitmap.Width / 2) * (bitmap.Height / 2);
            return (double)yellow / total;
        }
    }
}
