using SkiaSharp;
using Topten.RichTextKit;
using VecTiles.Renderers.Skia.Primitives;

namespace VecTiles.Renderers.Skia.Test;

/// <summary>
/// Unit tests for the layout cache of <see cref="RenderedTextCache" />.
/// </summary>
public class RenderedTextCacheTests : IDisposable
{
    /// <summary>
    /// Resets the font cache so global registrations do not leak into other test classes.
    /// </summary>
    public void Dispose()
    {
        FontCache.Reset();
    }

    /// <summary>
    /// Verifies that the layout of a cached <see cref="RenderedText" /> is not
    /// affected by creating another cache entry with a different font size for
    /// the same style name and font. The cache entries previously shared one
    /// mutable style instance, so the glyph size of the first entry's first
    /// paint silently followed the font size of the entry laid out last, while
    /// the glyph positions kept the layout size: the text appeared with
    /// unnaturally spread characters.
    /// </summary>
    [Fact]
    public void GetOrCreate_KeepsLayoutStyleOfExistingEntries()
    {
        FontCache.RegisterFontFile(GetFontPath("Roboto Regular.ttf"));

        var fontNames = new[] { "Roboto Regular" };
        var entry = RenderedTextCache.GetOrCreate("RenderedTextCacheRegression", "test_layer",
            fontNames, 22f, 1.2f, 0f, null, TextAlignment.Left, 0);
        var measuredWidth = entry.MeasuredWidth;

        // Same style name and font, but a different font size: this must not
        // touch the style of the first entry
        RenderedTextCache.GetOrCreate("RenderedTextCacheRegression", "test_layer",
            fontNames, 12f, 1.2f, 0f, null, TextAlignment.Left, 0);

        Assert.Equal(22f, entry.TextStyle.FontSize);
        Assert.Equal(measuredWidth, entry.MeasuredWidth);

        // The glyphs must be painted at the layout size, not at the font size
        // of the entry laid out last: the caps height of Roboto at 22 px is
        // about 15-16 px, at 12 px about 8-9 px
        Assert.InRange(MeasureInkHeight(entry), 13f, 19f);
    }

    /// <summary>
    /// Paints the entry on a white bitmap and measures the height of the ink.
    /// </summary>
    private static float MeasureInkHeight(RenderedText entry)
    {
        const int size = 80;
        using var bitmap = new SKBitmap(size, size);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            canvas.Translate(10, 30);
            entry.TextStyle.TextColor = SKColors.Black;
            entry.TextStyle.HaloWidth = 0;
            entry.Text.Paint(canvas);
        }

        var top = -1;
        var bottom = -1;
        var pixels = bitmap.Pixels;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                if (pixels[y * size + x].Red < 128)
                {
                    if (top < 0)
                    {
                        top = y;
                    }
                    bottom = y;
                    break;
                }
            }
        }

        Assert.True(top >= 0, "No ink painted");

        // caps height in pixels: the ink height of an entry laid out at a
        // smaller font size would fall clearly below the font size
        return bottom - top + 1;
    }

    private static string GetFontPath(string fileName)
    {
        return Path.Combine(Utilities.DataFiles, "Fonts", fileName);
    }
}
