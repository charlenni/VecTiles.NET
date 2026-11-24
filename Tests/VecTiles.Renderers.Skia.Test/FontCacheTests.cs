using SkiaSharp;
using VecTiles.Renderers.Skia.Primitives;

namespace VecTiles.Renderers.Skia.Test;

/// <summary>
/// Unit tests for the font lookup, registration and fallback behavior of <see cref="FontCache" />.
/// </summary>
public class FontCacheTests : IDisposable
{
    /// <summary>
    /// Resets the font cache so global registrations do not leak into other test classes.
    /// </summary>
    public void Dispose()
    {
        FontCache.Reset();
    }

    /// <summary>
    /// Verifies that <see cref="FontCache.GetMissingFonts" /> reports family names that cannot be resolved.
    /// </summary>
    [Fact]
    public void GetMissingFonts_ReportsUnresolvableFamilies()
    {
        var missing = FontCache.GetMissingFonts(["This Family Does Not Exist 12345"]);

        Assert.Contains("This Family Does Not Exist 12345", missing);
    }

    /// <summary>
    /// Verifies that a family registered via <see cref="FontCache.RegisterFontFile" /> is not reported as missing.
    /// </summary>
    [Fact]
    public void GetMissingFonts_AcceptsRegisteredFontFiles()
    {
        var typeface = SKTypeface.FromFile(GetFontPath("Roboto Medium.ttf"));
        Assert.NotNull(typeface);

        FontCache.RegisterFontFile(GetFontPath("Roboto Medium.ttf"));

        var missing = FontCache.GetMissingFonts([typeface!.FamilyName]);

        Assert.Empty(missing);
    }

    /// <summary>
    /// Verifies that a family from a font search path becomes resolvable on the next lookup.
    /// </summary>
    [Fact]
    public void AddFontSearchPath_MakesFamilyAvailableLazily()
    {
        var typeface = SKTypeface.FromFile(GetFontPath("Roboto Regular.ttf"));
        Assert.NotNull(typeface);

        FontCache.AddFontSearchPath("Files/OpenMapTiles/Fonts", searchSubDirectories: false);

        var missing = FontCache.GetMissingFonts([typeface!.FamilyName]);

        Assert.Empty(missing);
    }

    /// <summary>
    /// Verifies that a style created from a family resolved via a search path keeps the
    /// original family name.
    /// </summary>
    [Fact]
    public void AddFontSearchPath_BeforeStyleLoad_KeepsOriginalFamilyName()
    {
        var typeface = SKTypeface.FromFile(GetFontPath("Roboto Regular.ttf"));
        Assert.NotNull(typeface);

        FontCache.AddFontSearchPath("Files/OpenMapTiles/Fonts", searchSubDirectories: false);

        var style = FontCache.CreateFonts($"FontCacheTests_{Guid.NewGuid():N}", [typeface!.FamilyName]);
        Assert.NotNull(style);
        Assert.Equal(typeface.FamilyName, style!.FontFamily);
    }

    /// <summary>
    /// Verifies that an unknown family falls back to the system default typeface.
    /// </summary>
    [Fact]
    public void CreateFonts_UnknownFamily_FallsBackToSystemDefault()
    {
        var style = FontCache.CreateFonts($"FontCacheTests_{Guid.NewGuid():N}", ["This Family Does Not Exist 12345"]);

        Assert.NotNull(style);
        Assert.Equal(SKTypeface.Default.FamilyName, style!.FontFamily);
    }

    /// <summary>
    /// Verifies that <see cref="FontCache.CreateFonts" /> with no names throws a <see cref="VecTiles.Common.Exceptions.FontNotFoundException" />.
    /// </summary>
    [Fact]
    public void CreateFonts_EmptyNames_ThrowsFontNotFound()
    {
        Assert.Throws<VecTiles.Common.Exceptions.FontNotFoundException>(() => FontCache.CreateFonts($"FontCacheTests_{Guid.NewGuid():N}", []));
    }

    /// <summary>
    /// Verifies that a missing family is loaded via the configured font source resolver
    /// and that the resolver is only consulted once.
    /// </summary>
    [Fact]
    public void CreateFonts_MissingFont_LoadsFontFromSourceResolver()
    {
        // Bungee is an open source font (OFL) that is not installed on the
        // system, so the font file must be loaded via the resolver
        var calls = 0;
        FontCache.SetFontSourceResolver(name =>
        {
            calls++;
            return name == "Bungee"
                ? "file://" + Path.GetFullPath(GetFontPath("Bungee Regular.ttf"))
                : null;
        });

        var style = FontCache.CreateFonts($"FontCacheTests_{Guid.NewGuid():N}", ["Bungee"]);

        Assert.NotNull(style);
        Assert.Equal("Bungee", style!.FontFamily);
        Assert.Equal(1, calls);
    }

    /// <summary>
    /// Verifies that a source resolver returning no match falls back to the system default typeface.
    /// </summary>
    [Fact]
    public void CreateFonts_SourceResolverReturnsNull_FallsBackToSystemDefault()
    {
        FontCache.SetFontSourceResolver(_ => null);

        var style = FontCache.CreateFonts($"FontCacheTests_{Guid.NewGuid():N}", ["This Family Does Not Exist 12345"]);

        Assert.NotNull(style);
        Assert.Equal(SKTypeface.Default.FamilyName, style!.FontFamily);
    }

    /// <summary>
    /// Verifies that the source resolver is consulted at most once per unknown family,
    /// even across repeated style creations.
    /// </summary>
    [Fact]
    public void CreateFonts_UnknownFamily_ConsultsSourceResolverOnlyOnce()
    {
        var calls = 0;
        FontCache.SetFontSourceResolver(_ => { calls++; return null; });

        FontCache.CreateFonts($"FontCacheTests_{Guid.NewGuid():N}", ["This Family Does Not Exist 12345"]);
        FontCache.CreateFonts($"FontCacheTests_{Guid.NewGuid():N}", ["This Family Does Not Exist 12345"]);

        Assert.Equal(1, calls);
    }

    private static string GetFontPath(string fileName)
    {
        return Path.Combine(Utilities.DataFiles, "Fonts", fileName);
    }
}
