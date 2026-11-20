using SkiaSharp;
using Topten.RichTextKit;
using VecTiles.Renderers.Skia.Primitives;
using Xunit.Abstractions;

namespace VecTiles.Renderers.Skia.Test;

/// <summary>
/// Diagnostic: compares the label width measured by the RichTextKit layout
/// (used for collision boxes) with the width of the glyphs drawn by
/// DrawTextOnPath (SKFont resolved via SKFontManager.MatchFamily).
/// </summary>
public class SchanzengrabenFontMeasureTests
{
    private readonly ITestOutputHelper _output;

    public SchanzengrabenFontMeasureTests(ITestOutputHelper output) => _output = output;

    [Theory]
    [InlineData(new[] { "Roboto Regular" }, 12f)]
    [InlineData(new[] { "Roboto Condensed Italic" }, 12f)]
    [InlineData(new[] { "Roboto Regular" }, 3.193f)]
    public void CompareMeasuredAndDrawnWidth(string[] fontNames, float fontSize)
    {
        FontCache.Reset();

        const string text = "Schanzengraben";

        var render = RenderedTextCache.GetOrCreate(text, "water_name_line", fontNames,
            fontSize, 1.2f, 0f, null, TextAlignment.Left, 0);

        var layoutFamily = render.TextStyle.FontFamily;

        // Typeface as resolved by the layout (RichTextKit + CachedFontMapper)
        var layoutTypeface = ((CachedFontMapper)CachedFontMapper.Instance).TypefaceFromStyle(
            render.TextStyle, false);

        // Typeface as resolved by the draw path (TextLineSymbolRenderer.ResolveTypeface)
        var drawTypeface = SKFontManager.Default.MatchFamily(layoutFamily,
            new SKFontStyle((SKFontStyleWeight)render.TextStyle.FontWeight,
                (SKFontStyleWidth)render.TextStyle.FontWidth,
                render.TextStyle.FontItalic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright));

        _output.WriteLine($"fontSize={fontSize}");
        _output.WriteLine($"style font family '{layoutFamily}' weight {render.TextStyle.FontWeight} width {render.TextStyle.FontWidth} italic {render.TextStyle.FontItalic}");
        _output.WriteLine($"layout typeface: '{layoutTypeface!.FamilyName}' weight {layoutTypeface.FontWeight} width {layoutTypeface.FontWidth} slant {layoutTypeface.FontSlant}");
        _output.WriteLine($"draw   typeface: '{drawTypeface!.FamilyName}' weight {drawTypeface.FontWeight} width {drawTypeface.FontWidth} slant {drawTypeface.FontSlant}");
        _output.WriteLine($"RichTextKit MeasuredWidth = {render.MeasuredWidth:F2}");

        foreach (var hinting in new[] { SKFontHinting.None, SKFontHinting.Slight, SKFontHinting.Normal, SKFontHinting.Full })
        {
            foreach (var subpixel in new[] { false, true })
            {
                using var font = new SKFont(drawTypeface, fontSize)
                {
                    Hinting = hinting,
                    Subpixel = subpixel,
                };

                // Per-character sum exactly like the DrawTextOnPath extension
                var perCharWidth = text.Sum(c => font.MeasureText(c.ToString()));
                var wholeWidth = font.MeasureText(text);

                _output.WriteLine($"  hinting={hinting} subpixel={subpixel}: whole={wholeWidth:F2} per-char-sum={perCharWidth:F2}");

                if (hinting == SKFontHinting.None && !subpixel)
                {
                    var advances = text.Select(c => font.MeasureText(c.ToString()));
                    _output.WriteLine("    per-char advances: " + string.Join(", ", advances.Select(a => a.ToString("F2"))));
                }
            }
        }
    }
}
