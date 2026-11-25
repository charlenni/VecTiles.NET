using BruTile;
using SkiaSharp;
using VecTiles.Common.Interfaces;
using VecTiles.Converters.Mapsforge;
using VecTiles.Controls.Mapsui;
using Viewport = Mapsui.Viewport;

namespace VecTiles.Renderers.Skia.Test;

/// <summary>
/// Regression test of the full Mapsforge draw pipeline on a plain canvas
/// (density 1, canonical resolution of the tile zoom): renders the
/// Quaibruecke area of zuerich.map at zoom 16 with the Mapsforge default
/// theme through <see cref="RenderedTileLayerRenderer"/> and checks that
/// the primary/secondary strokes (#FFD700) come out at the theme width
/// (about 7.5 px at zoom 16 -- a doubled stroke would roughly double the
/// yellow pixel count).
/// </summary>
public class MFStrokeWidthAppPipelineTests
{
    private static readonly string _mapFile = Path.Combine(Utilities.DataFiles, "..",
        "Mapsforge", "zuerich.map");
    private static readonly string _mapsforgeTheme = Path.Combine(Utilities.DataFiles, "..",
        "Mapsforge", "mapsforge", "default.xml");

    [Fact]
    public async Task QuaibrueckeStrokesAtZoom16StayAtThemeWidth()
    {
        const int zoom = 16;
        const int tileSize = 512;
        const int TileX = 34323; // Zuerich, Buerkliplatz / Quaibruecke
        const int TileY = 22951;

        // Canonical resolution of a 512 px tile (EPSG:3857), so the tile px
        // map 1:1 onto the canvas px and a stroke of the theme width covers
        // exactly its width in px
        double resolution = 78271.51696402031 / Math.Pow(2, zoom);
        double tileSpan = tileSize * resolution;

        double centerXWorld = -20037508.343 + (TileX + 0.5) * tileSpan;
        double centerYWorld = 20037508.343 - (TileY + 0.5) * tileSpan;
        var viewport = new Viewport(centerXWorld, centerYWorld, resolution, 0, 3 * tileSize, 3 * tileSize);

        using var tileSource = new MFRenderedTileSource(_mapFile, new IdentityTagsConverter("mapsforge"), _mapsforgeTheme);
        var tileLayer = new RenderedTileLayer(tileSource) { SynchronousPlacement = true };
        var renderService = new Mapsui.Rendering.RenderService();

        using var bitmap = new SKBitmap(3 * tileSize, 3 * tileSize);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        RenderedTileLayerRenderer.Draw(canvas, viewport, tileLayer, renderService);
        RenderedTileLayerRenderer.Draw(canvas, viewport, tileLayer, renderService);

        // Count the yellow (#FFD700) pixels on a sparse grid: the tile set
        // around the Quaibruecke carries several primary/secondary roads.
        // Measured with the theme-correct stroke widths: about 28000 px
        // within the full raster; a stroke drawn 2x too wide would push the
        // count far above 45000 (the wide roads dominate the yellow area).
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

        // Full grid equivalent of the sparse count (x2 per axis)
        var fullGridEstimate = yellow * 4;

        Assert.InRange(fullGridEstimate, 15000, 40000);

        await Task.CompletedTask;
    }
}
