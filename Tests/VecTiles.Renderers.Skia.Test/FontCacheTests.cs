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

        FontCache.AddFontSearchPath(Path.Combine(Utilities.DataFiles, "Fonts"), searchSubDirectories: false);

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

        FontCache.AddFontSearchPath(Path.Combine(Utilities.DataFiles, "Fonts"), searchSubDirectories: false);

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
    /// Verifies that <see cref="FontCache.CreateFonts" /> tries every candidate of
    /// the font name list before falling back to the system default. Previously
    /// the first unresolvable name produced the fallback style immediately, so
    /// the remaining candidates were never tried (e.g. the Mapsforge default
    /// font list "Open Sans / Arial Unicode MS / Roboto" always ended in the
    /// system default, a serif font on many systems).
    /// </summary>
    [Fact]
    public void CreateFonts_TriesAllCandidates_BeforeSystemDefault()
    {
        FontCache.RegisterFontFile(GetFontPath("Roboto Regular.ttf"));

        var style = FontCache.CreateFonts(
            $"FontCacheTests_{Guid.NewGuid():N}",
            ["This Family Does Not Exist 12345", "Roboto"]);

        Assert.NotNull(style);
        Assert.Equal("Roboto", style!.FontFamily);
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

    /// <summary>
    /// Verifies that a font name resolver maps a style file name to a
    /// registered family, also when the resolver is set after the style was
    /// already resolved to the system default.
    /// </summary>
    [Fact]
    public void SetFontNameResolver_RenamesStyleFontName()
    {
        FontCache.RegisterFontFile(GetFontPath("Roboto Regular.ttf"));

        var styleName = $"FontCacheTests_{Guid.NewGuid():N}";

        var styleBefore = FontCache.CreateFonts(styleName, ["noto_sans_regular"]);
        Assert.NotNull(styleBefore);
        Assert.Equal(SKTypeface.Default.FamilyName, styleBefore!.FontFamily);

        FontCache.SetFontNameResolver(f => f switch
        {
            "noto_sans_regular" => "Roboto Regular",
            _ => f
        });

        var styleAfter = FontCache.CreateFonts(styleName, ["noto_sans_regular"]);
        Assert.NotNull(styleAfter);
        Assert.Equal("Roboto", styleAfter!.FontFamily);
    }

    /// <summary>
    /// Verifies that the style attributes (weight, width, italic) are parsed
    /// from the name returned by the resolver, not from the original style
    /// file name.
    /// </summary>
    [Fact]
    public void SetFontNameResolver_ParsesStyleAttributesOfResolvedName()
    {
        var typeface = SKTypeface.FromFile(GetFontPath("Roboto Medium.ttf"));
        Assert.NotNull(typeface);

        FontCache.RegisterFontFile(GetFontPath("Roboto Medium.ttf"));
        FontCache.SetFontNameResolver(f => f switch
        {
            "noto_sans_medium" => "Roboto Medium",
            _ => f
        });

        var style = FontCache.CreateFonts($"FontCacheTests_{Guid.NewGuid():N}", ["noto_sans_medium"]);

        Assert.NotNull(style);
        Assert.Equal("Roboto", style!.FontFamily);
        Assert.Equal(500, style.FontWeight);
    }

    /// <summary>
    /// Verifies that a resolver returning null or an empty string leaves the
    /// font name unchanged, so it falls back to the system default.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void SetFontNameResolver_ReturnsNullOrEmpty_KeepsOriginalName(string? resolvedName)
    {
        FontCache.SetFontNameResolver(_ => resolvedName);

        var style = FontCache.CreateFonts($"FontCacheTests_{Guid.NewGuid():N}", ["This Family Does Not Exist 12345"]);

        Assert.NotNull(style);
        Assert.Equal(SKTypeface.Default.FamilyName, style!.FontFamily);
    }

    /// <summary>
    /// Verifies that a throwing resolver does not break the style creation
    /// and leaves the font name unchanged.
    /// </summary>
    [Fact]
    public void SetFontNameResolver_Throws_KeepsOriginalName()
    {
        FontCache.SetFontNameResolver(_ => throw new InvalidOperationException("resolver failed"));

        var style = FontCache.CreateFonts($"FontCacheTests_{Guid.NewGuid():N}", ["This Family Does Not Exist 12345"]);

        Assert.NotNull(style);
        Assert.Equal(SKTypeface.Default.FamilyName, style!.FontFamily);
    }

    /// <summary>
    /// Verifies that removing the name resolver restores the fallback to the
    /// system default, even for an already created style.
    /// </summary>
    [Fact]
    public void SetFontNameResolver_Null_RemovesResolver()
    {
        FontCache.RegisterFontFile(GetFontPath("Roboto Regular.ttf"));

        var styleName = $"FontCacheTests_{Guid.NewGuid():N}";
        FontCache.SetFontNameResolver(_ => "Roboto Regular");
        var styleWithResolver = FontCache.CreateFonts(styleName, ["noto_sans_regular"]);
        Assert.NotNull(styleWithResolver);
        Assert.Equal("Roboto", styleWithResolver!.FontFamily);

        FontCache.SetFontNameResolver(null);

        var styleWithoutResolver = FontCache.CreateFonts(styleName, ["noto_sans_regular"]);
        Assert.NotNull(styleWithoutResolver);
        Assert.Equal(SKTypeface.Default.FamilyName, styleWithoutResolver!.FontFamily);
    }

    /// <summary>
    /// Verifies that <see cref="FontCache.GetMissingFonts" /> checks the
    /// resolved name, so a renamed and available font is not reported as missing.
    /// </summary>
    [Fact]
    public void GetMissingFonts_AppliesNameResolver()
    {
        FontCache.RegisterFontFile(GetFontPath("Roboto Regular.ttf"));
        FontCache.SetFontNameResolver(f => f switch
        {
            "noto_sans_regular" => "Roboto Regular",
            _ => f
        });

        var missing = FontCache.GetMissingFonts(["noto_sans_regular"]);

        Assert.Empty(missing);
    }

    private static string GetFontPath(string fileName)
    {
        return Path.Combine(Utilities.DataFiles, "Fonts", fileName);
    }
}
