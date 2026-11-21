using SkiaSharp;
using Xunit.Abstractions;

namespace VecTiles.Renderers.Skia.Test;

/// <summary>
/// Helper (no assertion): crops the label region of the app pipeline frames and
/// scales it up, so the drawn glyphs can be compared with the debug borders.
/// </summary>
public class CropFramesTests
{
    private readonly ITestOutputHelper _output;

    public CropFramesTests(ITestOutputHelper output) => _output = output;

    [Theory]
    [InlineData("17,0", 520, 480, 320, 250)]
    [InlineData("17,4", 560, 560, 320, 250)]
    [InlineData("17,8", 800, 30, 320, 250)]
    public void CropLabelRegion(string frame, int x, int y, int width, int height)
    {
        var results = Path.Combine(Utilities.TestFiles, "Results");
        var sourcePath = Path.Combine(results, $"schanzengraben-app-pipeline-{frame}.png");

        if (!File.Exists(sourcePath))
        {
            _output.WriteLine($"missing: {sourcePath}");
            return;
        }

        using var source = SKBitmap.Decode(sourcePath);

        using var bitmap = new SKBitmap(width * 3, height * 3);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.LightGray);
            canvas.Scale(3f, 3f);
            canvas.DrawBitmap(source, new SKRect(x, y, x + width, y + height),
                new SKRect(0, 0, width, height));
        }

        var path = Path.Combine(results, $"crop-label-{frame}.png");
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.OpenWrite(path);
        data.SaveTo(stream);

        _output.WriteLine($"written: {path}");
    }
}
