using BruTile;
using Mapsui.Extensions;
using SkiaSharp;
using VecTiles.Controls.Mapsui;
using VecTiles.Controls.Mapsui.Extensions;
using Xunit.Abstractions;

namespace VecTiles.Renderers.Skia.Test;

/// <summary>
/// Diagnostic: renders frames through the exact pipeline of the app
/// (RenderedSymbolsLayerRenderer with placement at the canonical integer zoom
/// and drawing at the fractional zoom of the viewport) and writes debug
/// borders around the drawn symbols, to compare the drawn label run with the
/// collision boxes.
/// </summary>
public class SchanzengrabenAppPipelineTests
{
    private const string _path = "Files";

    private readonly ITestOutputHelper _output;

    public SchanzengrabenAppPipelineTests(ITestOutputHelper output) => _output = output;

    private const double WorldWidth = 40075016.686;
    private const double WorldHalf = WorldWidth / 2;

    [Theory]
    [InlineData(17.0)]
    [InlineData(17.2)]
    [InlineData(17.4)]
    [InlineData(17.6)]
    [InlineData(17.8)]
    public void RenderAppPipelineFrame(double fractionalZoom)
    {
        var tileSource = new OMTRenderedTileSource($"file://{_path}/osm-liberty.json");
        using var layer = new RenderedSymbolsLayer(tileSource)
        {
            ShowValidBorders = true,
            ShowInvalidBorders = true,
        };

        var zoomLevel = (int)Math.Floor(fractionalZoom + 1e-6);
        double resolution = fractionalZoom.ToResolution();
        double tileSpan = WorldWidth / Math.Pow(2, 17);

        // Center on the middle of tile 17/68642/45902 (XYZ), like the user's frame
        double centerX = -WorldHalf + (68642 + 0.5) * tileSpan;
        double centerY = WorldHalf - (45902 + 0.5) * tileSpan;
        var viewport = new Mapsui.Viewport(centerX, centerY, resolution, 0, 1024, 768);

        using var bitmap = new SKBitmap(1024, 768);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        RenderedSymbolsLayerRenderer.Draw(canvas, viewport, layer, new Mapsui.Rendering.RenderService());

        canvas.Flush();

        var path = Path.Combine(_path, "Results", $"schanzengraben-app-pipeline-{fractionalZoom:F1}.png");
        Directory.CreateDirectory(Path.Combine(_path, "Results"));
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.OpenWrite(path);
        data.SaveTo(stream);

        _output.WriteLine($"written: {path}");
    }
}
