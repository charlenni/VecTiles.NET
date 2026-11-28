using NetTopologySuite.Geometries;
using SkiaSharp;
using VecTiles.Common.Primitives;
using VecTiles.Renderers.Skia;
using VecTiles.Renderers.Skia.Primitives;

namespace VecTiles.Renderers.Skia.Test;

/// <summary>
/// Tests for drawing text along a path.
/// </summary>
public class TextLineSymbolRendererTests : IDisposable
{
    /// <summary>
    /// Resets the font cache so global registrations do not leak into other test classes.
    /// </summary>
    public void Dispose()
    {
        FontCache.Reset();
    }

    /// <summary>
    /// Verifies that text along a path is drawn with a font that was registered
    /// via <see cref="FontCache"/> (e.g. supplied by a font source resolver) and
    /// is not installed on the system. The typeface of drawn path text was
    /// resolved through the system font manager, which cannot see registered
    /// fonts, so such labels were silently not drawn.
    /// </summary>
    [Fact]
    public void DrawTextOnPath_DrawsWithRegisteredFont()
    {
        FontCache.RegisterFontFile(GetFontPath("Bungee Regular.ttf"));

        var geometry = new LineString(new[] { new Coordinate(0, 0), new Coordinate(1000, 0) });
        var symbol = new TextLineSymbol(new Tile(0, 0, 0), 1, geometry, "Regression")
        {
            StyleName = "test_layer",
            FontNames = new[] { "Bungee Regular" },
            FontSize = _ => 22f,
            Color = _ => Colors.Black,
            HaloWidth = _ => 0f,
            HaloColor = _ => Colors.White,
        };

        var context = new EvaluationContext(4, 1f, 0f);

        // Measures the label and creates the cached layout of the symbol
        var labelLength = TextLineSymbolRenderer.GetLabelLength(symbol, context);
        Assert.True(labelLength > 0);

        const int width = 320, height = 100;
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);

            using var path = new SKPath();
            path.MoveTo(10, 50);
            path.LineTo(width - 10, 50);

            var placement = new PathTextPlacement(Reversed: false, DrawStart: 10f, PathLength: 300f,
                Boxes: new[] { new Envelope(10, 60, 40, 60) });

            TextLineSymbolRenderer.DrawTextOnPath(canvas, context, symbol, path, placement);
            canvas.Flush();
        }

        var pixels = bitmap.Pixels;
        var inkPixels = pixels.Count(p => p.Red < 128);

        Assert.True(inkPixels > 20, $"No text drawn with registered font (label length {labelLength})");
    }

    private static string GetFontPath(string fileName)
    {
        return Path.Combine(Utilities.DataFiles, "Fonts", fileName);
    }
}
