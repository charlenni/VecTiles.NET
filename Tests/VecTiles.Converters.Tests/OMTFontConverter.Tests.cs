using VecTiles.Converters.SdfFont;

namespace VecTiles.Converters.Tests;

public class OMTFontConverterTests
{
    readonly string _path = "files\\fonts\\roboto condensed italic\\";

    [Fact]
    public async Task FontConverter_0_255_Test()
    {
        var data = File.ReadAllBytes(_path + "0-255.pbf");

        var fontConverter = new SdfFontConverter();
        var fontGlyphs = await fontConverter.Convert(data);

        Assert.True(fontGlyphs != null);
        Assert.True(fontGlyphs.Stacks != null);
        Assert.True(fontGlyphs.Stacks.Count == 1);
        Assert.True(fontGlyphs.Stacks[0] != null);
        Assert.True(fontGlyphs.Stacks[0].Name == "Roboto Condensed Italic");
        Assert.True(fontGlyphs.Stacks[0].Range == "0-255");
        Assert.True(fontGlyphs.Stacks[0].Glyph.Count == 194);

        Assert.True(fontGlyphs.Stacks[0].Glyph[20].Id == 49);
        Assert.True(fontGlyphs.Stacks[0].Glyph[20].Width == 6);
        Assert.True(fontGlyphs.Stacks[0].Glyph[20].Height == 17);
        Assert.True(fontGlyphs.Stacks[0].Glyph[20].Left == 6);
        Assert.True(fontGlyphs.Stacks[0].Glyph[20].Top == 15);
        Assert.True(fontGlyphs.Stacks[0].Glyph[20].Advance == 11);
        Assert.True(fontGlyphs.Stacks[0].Glyph[20].Bitmap.Length == 276);
        Assert.True(fontGlyphs.Stacks[0].Glyph[20].Bitmap[0] == 21);

        Assert.True(fontGlyphs.Stacks[0].Glyph[64].Id == 93);
        Assert.True(fontGlyphs.Stacks[0].Glyph[64].Width == 7);
        Assert.True(fontGlyphs.Stacks[0].Glyph[64].Height == 24);
        Assert.True(fontGlyphs.Stacks[0].Glyph[64].Left == 1);
        Assert.True(fontGlyphs.Stacks[0].Glyph[64].Top == 9);
        Assert.True(fontGlyphs.Stacks[0].Glyph[64].Advance == 5);
        Assert.True(fontGlyphs.Stacks[0].Glyph[64].Bitmap.Length == 390);
        Assert.True(fontGlyphs.Stacks[0].Glyph[64].Bitmap[0] == 0);
    }

    [Fact]
    public async Task FontConverter_1024_1279_Test()
    {
        var data = File.ReadAllBytes(_path + "1024-1279.pbf");

        var fontConverter = new SdfFontConverter();
        var fontGlyphs = await fontConverter.Convert(data);

        Assert.True(fontGlyphs != null);
        Assert.True(fontGlyphs.Stacks != null);
        Assert.True(fontGlyphs.Stacks.Count == 1);
        Assert.True(fontGlyphs.Stacks[0] != null);
        Assert.True(fontGlyphs.Stacks[0].Name == "Roboto Condensed Italic");
        Assert.True(fontGlyphs.Stacks[0].Range == "1024-1279");
        Assert.True(fontGlyphs.Stacks[0].Glyph.Count == 255);

        Assert.True(fontGlyphs.Stacks[0].Glyph[20].Id == 1044);
        Assert.True(fontGlyphs.Stacks[0].Glyph[20].Width == 15);
        Assert.True(fontGlyphs.Stacks[0].Glyph[20].Height == 21);
        Assert.True(fontGlyphs.Stacks[0].Glyph[20].Left == 1);
        Assert.True(fontGlyphs.Stacks[0].Glyph[20].Top == 15);
        Assert.True(fontGlyphs.Stacks[0].Glyph[20].Advance == 15);
        Assert.True(fontGlyphs.Stacks[0].Glyph[20].Bitmap.Length == 567);
        Assert.True(fontGlyphs.Stacks[0].Glyph[20].Bitmap[0] == 0);

        Assert.True(fontGlyphs.Stacks[0].Glyph[64].Id == 1088);
        Assert.True(fontGlyphs.Stacks[0].Glyph[64].Width == 11);
        Assert.True(fontGlyphs.Stacks[0].Glyph[64].Height == 18);
        Assert.True(fontGlyphs.Stacks[0].Glyph[64].Left == 1);
        Assert.True(fontGlyphs.Stacks[0].Glyph[64].Top == 23);
        Assert.True(fontGlyphs.Stacks[0].Glyph[64].Advance == 11);
        Assert.True(fontGlyphs.Stacks[0].Glyph[64].Bitmap.Length == 408);
        Assert.True(fontGlyphs.Stacks[0].Glyph[64].Bitmap[0] == 0);
    }
}
