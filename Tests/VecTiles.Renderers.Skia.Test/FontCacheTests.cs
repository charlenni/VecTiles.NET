using SkiaSharp;
using VecTiles.Renderers.Skia.Primitives;

namespace VecTiles.Renderers.Skia.Test;

public class FontCacheTests : IDisposable
{
    // Registrations are global, so they must not leak into other test classes
    // (e.g. the font tests comparing rendered bitmaps)
    public void Dispose()
    {
        FontCache.Reset();
    }

    [Fact]
    public void GetMissingFonts_ReportsUnresolvableFamilies()
    {
        var missing = FontCache.GetMissingFonts(["This Family Does Not Exist 12345"]);

        Assert.Contains("This Family Does Not Exist 12345", missing);
    }

    [Fact]
    public void GetMissingFonts_AcceptsRegisteredFontFiles()
    {
        var typeface = SKTypeface.FromFile(GetFontPath("Roboto Medium.ttf"));
        Assert.NotNull(typeface);

        FontCache.RegisterFontFile(GetFontPath("Roboto Medium.ttf"));

        var missing = FontCache.GetMissingFonts([typeface!.FamilyName]);

        Assert.Empty(missing);
    }

    [Fact]
    public void AddFontSearchPath_MakesFamilyAvailableLazily()
    {
        var typeface = SKTypeface.FromFile(GetFontPath("Roboto Regular.ttf"));
        Assert.NotNull(typeface);

        FontCache.AddFontSearchPath("Files/Fonts", searchSubDirectories: false);

        var missing = FontCache.GetMissingFonts([typeface!.FamilyName]);

        Assert.Empty(missing);
    }

    [Fact]
    public void AddFontSearchPath_BeforeStyleLoad_KeepsOriginalFamilyName()
    {
        var typeface = SKTypeface.FromFile(GetFontPath("Roboto Regular.ttf"));
        Assert.NotNull(typeface);

        FontCache.AddFontSearchPath("Files/Fonts", searchSubDirectories: false);

        var style = FontCache.CreateFonts($"FontCacheTests_{Guid.NewGuid():N}", [typeface!.FamilyName]);
        Assert.NotNull(style);
        Assert.Equal(typeface.FamilyName, style!.FontFamily);
    }

    [Fact]
    public void CreateFonts_UnknownFamily_FallsBackToSystemDefault()
    {
        var style = FontCache.CreateFonts($"FontCacheTests_{Guid.NewGuid():N}", ["This Family Does Not Exist 12345"]);

        Assert.NotNull(style);
        Assert.Equal(SKTypeface.Default.FamilyName, style!.FontFamily);
    }

    [Fact]
    public void CreateFonts_EmptyNames_ThrowsFontNotFound()
    {
        Assert.Throws<VecTiles.Common.Exceptions.FontNotFoundException>(() => FontCache.CreateFonts($"FontCacheTests_{Guid.NewGuid():N}", []));
    }

    private static string GetFontPath(string fileName)
    {
        return Path.Combine("Files", "Fonts", fileName);
    }
}
