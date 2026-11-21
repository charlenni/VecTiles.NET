using BruTile;
using VecTiles.Controls.Mapsui;
using SkiaSharp;
using VecTiles.Renderers.Skia.Test;
using Viewport = Mapsui.Viewport;

namespace VecTiles.Renderers.Skia.Test;

/// <summary>
/// Renders a given tile together with its surrounding tiles (3x3) and compares
/// the center tile against a golden image.
/// </summary>
/// <remarks>
/// Set environment variable VECTILES_RENDER_TEST_UPDATE_GOLDEN=true to recreate
/// the golden image from the current renderer output.
/// </remarks>
public class RenderedTileTests
{
    private static readonly string _path = Utilities.DataFiles;

    private readonly string OriginalsFolder = Path.Combine(Utilities.TestFiles, "Originals");
    private readonly string ResultsFolder = Path.Combine(Utilities.TestFiles, "Results");

    private const int Zoom = 19;
    private const int CenterX = 274582;
    private const int CenterY = 183615;
    private const int TileSize = 512;
    private const double WorldWidth = 40075016.686;
    private const double WorldHalf = WorldWidth / 2;

    private readonly string _goldenFile;
    private readonly string _resultFile;
    private readonly string _diffFile;

    public RenderedTileTests()
    {
        var name = $"RenderedTile-Z{Zoom}-X{CenterX}-Y{CenterY}";
        _goldenFile = Path.Combine(OriginalsFolder, name + ".png");
        _resultFile = Path.Combine(ResultsFolder, name + ".png");
        _diffFile = Path.Combine(ResultsFolder, name + "-diff.png");
    }

    [Fact]
    public void RenderTile_CenterTileMatchesGoldenImage()
    {
        using var rendered = RenderTileWithSurroundings(Zoom, CenterX, CenterY);
        Utilities.SaveBitmap(rendered, _resultFile);

        if (Environment.GetEnvironmentVariable("VECTILES_RENDER_TEST_UPDATE_GOLDEN") == "true")
        {
            Utilities.SaveBitmap(rendered, _goldenFile);
            return;
        }

        if (!File.Exists(_goldenFile))
        {
            Assert.Fail($"Golden image '{_goldenFile}' is missing. " +
                $"Current rendering was written to '{_resultFile}'. " +
                "Set VECTILES_RENDER_TEST_UPDATE_GOLDEN=true to create a new golden image.");
        }

        if (!Utilities.CompareBitmaps(_resultFile, _goldenFile))
        {
            Utilities.SaveBitmap(CreateDiffBitmap(_resultFile, _goldenFile), _diffFile);
            Assert.Fail($"Rendering differs from golden image. " +
                $"Actual: '{_resultFile}', golden: '{_goldenFile}', difference: '{_diffFile}'");
        }
    }

    /// <summary>
    /// Renders the given tile with the unified tile layer renderer on a canvas,
    /// surrounded by a ring of neighboring tiles (3x3), so that geometry and
    /// line symbols crossing the tile border are drawn correctly. The renderer
    /// draws style layer by style layer across all tiles and the placed symbols
    /// of the synchronous placement pass. Returns the cropped center tile.
    /// </summary>
    private static SKBitmap RenderTileWithSurroundings(int zoom, int centerX, int centerY)
    {
        const int size = 3 * TileSize;
        double resolution = 78271.51696402031 / Math.Pow(2, zoom); // initial resolution of a 512 tile in EPSG:3857
        double tileSpan = TileSize * resolution;

        double centerXWorld = -WorldHalf + (centerX + 0.5) * tileSpan;
        double centerYWorld = WorldHalf - (centerY + 0.5) * tileSpan;
        var viewport = new Viewport(centerXWorld, centerYWorld, resolution, 0, size, size);

        var styleSource = $"file://{_path}/osm-liberty.json";
        var tileSource = new OMTRenderedTileSource(styleSource);
        var tileLayer = new RenderedTileLayer(tileSource) { SynchronousPlacement = true };
        var renderService = new Mapsui.Rendering.RenderService();

        using var full = new SKBitmap(size, size);
        using (var canvas = new SKCanvas(full))
        {
            canvas.Clear(SKColors.Beige);

            RenderedTileLayerRenderer.Draw(canvas, viewport, tileLayer, renderService);
        }

        var cropped = new SKBitmap(TileSize, TileSize);
        using (var canvas = new SKCanvas(cropped))
        {
            canvas.DrawBitmap(full, new SKRect(TileSize, TileSize, 2 * TileSize, 2 * TileSize),
                new SKRect(0, 0, TileSize, TileSize));
        }
        return cropped;
    }

    private static SKBitmap CreateDiffBitmap(string resultPath, string goldenPath)
    {
        using var result = SKBitmap.Decode(resultPath);
        using var golden = SKBitmap.Decode(goldenPath);

        var diff = new SKBitmap(result.Width, result.Height);

        for (int x = 0; x < result.Width; x++)
        {
            for (int y = 0; y < result.Height; y++)
            {
                var actual = result.GetPixel(x, y);
                var expected = golden.GetPixel(x, y);

                if (actual == expected)
                {
                    diff.SetPixel(x, y, new SKColor((byte)(expected.Red / 3), (byte)(expected.Green / 3), (byte)(expected.Blue / 3)));
                }
                else
                {
                    diff.SetPixel(x, y, SKColors.Red);
                }
            }
        }

        return diff;
    }
}
