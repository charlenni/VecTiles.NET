using SkiaSharp;
using Topten.RichTextKit;
using VecTiles.Common.Exceptions;
using VecTiles.DataSources;

namespace VecTiles.Renderers.Skia.Primitives;

/// <summary>
/// Resolves the font names of a style file to styles for RichTextKit.
///
/// Resolution order per font name ("Noto Sans Bold"):
/// 1. Font files registered via <see cref="RegisterFontFile"/> or
///    <see cref="RegisterFontDirectory"/>,
/// 2. font files found in the directories added via <see cref="AddFontSearchPath"/>
///    (each directory is scanned once, lazily on first need),
/// 3. font files loaded for a missing font via the source resolver set by
///    <see cref="SetFontSourceResolver"/> (each font name is attempted once),
/// 4. fonts installed on the system,
/// 5. the system default family as last resort.
///
/// <see cref="GetMissingFonts"/> reports the font names of a style file that
/// resolve to the fallback (3), so the user can install them or register
/// matching font files before the map is displayed.
/// </summary>
public static class FontCache
{
    private static readonly Dictionary<(string, string, bool), Topten.RichTextKit.Style> TextStyles = new();
    private static readonly object TextStylesLock = new();

    // Font families registered from font files, keyed by family name
    private static readonly Dictionary<string, List<SKTypeface>> CustomTypefaces = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object CustomTypefacesLock = new();
    private static readonly string[] FontFileExtensions = [".ttf", ".otf", ".ttc"];

    // Directories to search for font files on first need
    private static readonly List<(string Path, bool Recursive)> FontSearchPaths = new();
    private static readonly HashSet<string> ScannedFontSearchPaths = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object FontSearchPathsLock = new();

    // Optional resolver that maps a font name to a source (URL notation of
    // ByteDataSource) from which the font file is loaded on first need
    private static Func<string, string?>? FontSourceResolver;
    private static readonly HashSet<string> AttemptedResolverFonts = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object FontSourceResolverLock = new();

    /// <summary>
    /// Sets an optional resolver that maps a font name to a source in URL
    /// notation (see <see cref="ByteDataSource.GetBytesAsync"/>), from which
    /// the font file is loaded when the font is neither registered nor
    /// installed on the system. Returning null or an empty string, or having
    /// no resolver set, leaves the font to the usual resolution order
    /// (registered files, search paths, system fonts, system default).
    /// Call with null to remove the resolver again.
    /// </summary>
    public static void SetFontSourceResolver(Func<string, string?>? fontSourceResolver)
    {
        lock (FontSourceResolverLock)
        {
            FontSourceResolver = fontSourceResolver;
            AttemptedResolverFonts.Clear();
        }

        // Styles already resolved to the system default must be recalculated,
        // so fonts loaded through the resolver are taken into account
        lock (TextStylesLock)
        {
            TextStyles.Clear();
        }
    }

    /// <summary>
    /// Creates a text style with the first font of the given font name list
    /// that can be resolved, in the order given. Only if none of the names
    /// resolves to a font, the system default family is used as last resort
    /// (see the resolution order in the class description).
    /// </summary>
    /// <param name="styleName">Name of the style, used for error messages.</param>
    /// <param name="names">Font family names to try, in order.</param>
    /// <returns>The created text style.</returns>
    /// <exception cref="FontNotFoundException">No font of the list could be resolved.</exception>
    public static Style? CreateFonts(string styleName, string[] names)
    {
        if (names == null || names.Length == 0)
        {
            throw new FontNotFoundException($"Font of style not found");
        }

        foreach (var name in names)
        {
            var textStyle = CreateFont(styleName, name, allowSystemDefaultFallback: false);

            if (textStyle != null)
            {
                return textStyle;
            }
        }

        // None of the candidates resolved: fall back to the system default,
        // so labels stay visible even without the styled fonts installed
        var fallbackName = names.FirstOrDefault(name => !string.IsNullOrEmpty(name));

        if (fallbackName == null)
        {
            throw new FontNotFoundException($"Font of style not found");
        }

        return CreateFont(styleName, fallbackName, allowSystemDefaultFallback: true);
    }

    /// <summary>
    /// Clears all cached styles, registered font files, font search paths,
    /// attempted font source resolutions and the font source resolver itself,
    /// so that fonts registered after a style was loaded are taken into
    /// account on the next style creation.
    /// </summary>
    public static void Reset()
    {
        lock (TextStylesLock)
        {
            TextStyles.Clear();
        }

        lock (CustomTypefacesLock)
        {
            CustomTypefaces.Clear();
        }

        lock (FontSearchPathsLock)
        {
            FontSearchPaths.Clear();
            ScannedFontSearchPaths.Clear();
        }

        lock (FontSourceResolverLock)
        {
            FontSourceResolver = null;
            AttemptedResolverFonts.Clear();
        }
    }

    /// <summary>
    /// Registers a single font file (*.ttf, *.otf, *.ttc), so that its family
    /// can be resolved even if it is not installed on the system.
    /// </summary>
    public static void RegisterFontFile(string path)
    {
        var typeface = SKTypeface.FromFile(path)
            ?? throw new FontNotFoundException($"Font file '{path}' could not be loaded");

        AddCustomTypeface(typeface);
    }

    /// <summary>
    /// Registers a font from its file content (*.ttf, *.otf, *.ttc), so that
    /// its family can be resolved even if it is not installed on the system.
    /// </summary>
    public static void RegisterFontData(byte[] data)
    {
        var typeface = SKTypeface.FromStream(new MemoryStream(data))
            ?? throw new FontNotFoundException($"Font data ({data.Length} bytes) could not be loaded");

        AddCustomTypeface(typeface);
    }

    /// <summary>
    /// Registers all font files (*.ttf, *.otf, *.ttc) of a directory, so that
    /// their families can be resolved even if they are not installed on the
    /// system.
    /// </summary>
    /// <returns>The number of registered font files.</returns>
    public static int RegisterFontDirectory(string path, bool searchSubDirectories = true)
    {
        var searchOption = searchSubDirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

        var files = FontFileExtensions
            .SelectMany(extension => Directory.EnumerateFiles(path, $"*{extension}", searchOption))
            .Distinct();

        var count = 0;

        foreach (var file in files)
        {
            try
            {
                RegisterFontFile(file);
                count++;
            }
            catch (FontNotFoundException)
            {
                // Unreadable file: skip it, the remaining files still register
            }
        }

        return count;
    }

    /// <summary>
    /// Adds a directory that is searched for font files (*.ttf, *.otf, *.ttc)
    /// on first need, so that their families can be resolved even if they are
    /// not installed on the system. The directory is scanned once, lazily when
    /// the first font family is missing; already registered families stay
    /// untouched.
    /// </summary>
    public static void AddFontSearchPath(string path, bool searchSubDirectories = true)
    {
        lock (FontSearchPathsLock)
        {
            if (!ScannedFontSearchPaths.Contains(path))
            {
                FontSearchPaths.Add((path, searchSubDirectories));
            }
        }
    }

    /// <summary>
    /// Scans all directories added via <see cref="AddFontSearchPath"/> once and
    /// registers every font file found.
    /// </summary>
    private static void EnsureFontSearchPathsScanned()
    {
        List<(string Path, bool Recursive)> paths;

        lock (FontSearchPathsLock)
        {
            if (FontSearchPaths.Count == 0)
            {
                return;
            }

            paths = new List<(string, bool)>(FontSearchPaths);
        }

        foreach (var (path, recursive) in paths)
        {
            lock (FontSearchPathsLock)
            {
                if (ScannedFontSearchPaths.Contains(path))
                {
                    continue;
                }

                // Marked as scanned before enumerating, so a concurrent call
                // does not scan the same directory twice
                ScannedFontSearchPaths.Add(path);
            }

            if (!Directory.Exists(path))
            {
                continue;
            }

            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

            var files = FontFileExtensions
                .SelectMany(extension => Directory.EnumerateFiles(path, $"*{extension}", searchOption))
                .Distinct();

            foreach (var file in files)
            {
                try
                {
                    RegisterFontFile(file);
                }
                catch (FontNotFoundException)
                {
                    // Unreadable file: skip it, the remaining files still register
                }
            }
        }
    }

    /// <summary>
    /// Checks which of the given font names (as in the style file) resolve to
    /// a real font, either registered via <see cref="RegisterFontFile"/>/
    /// <see cref="RegisterFontDirectory"/> or installed on the system.
    /// </summary>
    /// <returns>The font names that fall back to the system default family.</returns>
    public static IReadOnlyList<string> GetMissingFonts(IEnumerable<string> fontNames)
    {
        var missing = new List<string>();

        foreach (var name in fontNames.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            var textStyle = new Topten.RichTextKit.Style();
            var family = name.CreateFontWidth(ref textStyle)
                .CreateFontWeight(ref textStyle)
                .CreateFontItalic(ref textStyle);

            if (!IsAvailable(family))
            {
                missing.Add(name);
            }
        }

        return missing;
    }

    /// <summary>
    /// Writes all missing font names to the console, so that the user knows
    /// which fonts to install or which font files to register.
    /// </summary>
    public static void ReportMissingFonts(string styleName, IEnumerable<string> fontNames)
    {
        var missing = GetMissingFonts(fontNames);

        if (missing.Count == 0)
        {
            return;
        }

        Console.WriteLine($"Style '{styleName}': font(s) not found, labels are drawn with the system default font instead:");
        foreach (var name in missing)
        {
            Console.WriteLine($"  {name}");
        }
        Console.WriteLine("Install the fonts or register font files via FontCache.AddFontSearchPath/RegisterFontFile/RegisterFontDirectory.");
    }

    /// <summary>
    /// Resolves a custom registered typeface for the given family and style
    /// attributes. Called by the font mapper before falling back to system
    /// fonts.
    /// </summary>
    internal static bool TryGetCustomTypeface(string? fontFamily, int fontWeight, bool fontItalic, out SKTypeface typeface)
    {
        typeface = null!;

        if (string.IsNullOrEmpty(fontFamily))
        {
            return false;
        }

        EnsureFontSearchPathsScanned();

        lock (CustomTypefacesLock)
        {
            if (!CustomTypefaces.TryGetValue(fontFamily, out var candidates))
            {
                return false;
            }

            typeface = SelectTypeface(candidates, fontWeight, fontItalic);
            return true;
        }
    }

    private static void AddCustomTypeface(SKTypeface typeface)
    {
        lock (CustomTypefacesLock)
        {
            if (!CustomTypefaces.TryGetValue(typeface.FamilyName, out var list))
            {
                list = new List<SKTypeface>();
                CustomTypefaces[typeface.FamilyName] = list;
            }

            // Multiple files of the same family may be registered (e.g. all
            // weights of Noto Sans); keep each face only once
            if (!list.Any(t => t.FontWeight == typeface.FontWeight
                && t.FontWidth == typeface.FontWidth
                && t.FontSlant == typeface.FontSlant))
            {
                list.Add(typeface);
            }
        }
    }

    private static bool IsAvailable(string family)
    {
        // Font search paths are scanned on first need, so that font files of
        // added directories are found before falling back to the system fonts
        EnsureFontSearchPathsScanned();

        lock (CustomTypefacesLock)
        {
            if (CustomTypefaces.ContainsKey(family))
            {
                return true;
            }
        }

        var typeface = SKFontManager.Default.MatchFamily(family);
        return typeface != null && typeface.FamilyName.Equals(family, StringComparison.OrdinalIgnoreCase);
    }

    private static SKTypeface SelectTypeface(List<SKTypeface> candidates, int fontWeight, bool fontItalic)
    {
        var slant = fontItalic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright;

        // Exact match of weight and slant
        var match = candidates.FirstOrDefault(t => t.FontWeight == fontWeight && t.FontSlant == slant);

        // Any face with the requested slant, closest in weight
        match ??= candidates
            .Where(t => t.FontSlant == slant)
            .OrderBy(t => Math.Abs(t.FontWeight - fontWeight))
            .FirstOrDefault();

        // Any face at all, closest in weight
        match ??= candidates.OrderBy(t => Math.Abs(t.FontWeight - fontWeight)).First();

        return match;
    }

    private static Topten.RichTextKit.Style? CreateFont(string styleName, string fontName,
        bool allowSystemDefaultFallback = true)
    {
        if (string.IsNullOrEmpty(fontName))
        {
            return null;
        }

        var key = (styleName, fontName, allowSystemDefaultFallback);

        lock (TextStylesLock)
        {
            if (TextStyles.TryGetValue(key, out var textStyle))
            {
                return textStyle;
            }
        }

        // A font that is neither registered nor installed may be loadable via
        // the font source resolver before it is resolved to the system default
        TryLoadFontFromResolver(fontName);

        lock (TextStylesLock)
        {
            if (TextStyles.TryGetValue(key, out var textStyle))
            {
                return textStyle;
            }

            textStyle = new Topten.RichTextKit.Style();

            var fontFamilyName = fontName.CreateFontWidth(ref textStyle)
                .CreateFontWeight(ref textStyle)
                .CreateFontItalic(ref textStyle);

            // Since registered font files are resolved by the font mapper, the
            // family name is kept as is; unknown families return null here, so
            // the next candidate of the font name list can be tried. Only the
            // last resort (see CreateFonts) falls back to the system default,
            // so labels stay visible even without the styled fonts installed
            if (!IsAvailable(fontFamilyName))
            {
                if (!allowSystemDefaultFallback)
                {
                    return null;
                }

                fontFamilyName = SKTypeface.Default.FamilyName;
            }

            textStyle.FontFamily = fontFamilyName;

            TextStyles.Add(key, textStyle);

            return textStyle;
        }
    }

    /// <summary>
    /// Loads a font file via the font source resolver, if a resolver is set,
    /// the font was not attempted before and it is still missing after the
    /// usual resolution. A failure of the resolver or of the download is
    /// reported to the console and leaves the font to the system default, so
    /// labels stay visible even if the font cannot be loaded.
    /// </summary>
    private static void TryLoadFontFromResolver(string fontName)
    {
        Func<string, string?>? resolver;

        lock (FontSourceResolverLock)
        {
            resolver = FontSourceResolver;
        }

        if (resolver is null || string.IsNullOrWhiteSpace(fontName))
        {
            return;
        }

        // Font search paths are scanned on first need, so font files of added
        // directories are found before consulting the resolver
        EnsureFontSearchPathsScanned();

        // The resolver receives the font name as in the style file (e.g.
        // "Noto Sans Bold"); the family without style attributes decides
        // whether the font is really missing
        var probeStyle = new Topten.RichTextKit.Style();
        var family = fontName.CreateFontWidth(ref probeStyle)
            .CreateFontWeight(ref probeStyle)
            .CreateFontItalic(ref probeStyle);

        if (IsAvailable(family))
        {
            return;
        }

        lock (FontSourceResolverLock)
        {
            // Only one attempt per font name, even if it stays unsuccessful
            if (!AttemptedResolverFonts.Add(fontName))
            {
                return;
            }
        }

        string? source;

        try
        {
            source = resolver(fontName);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Could not resolve a source for font '{fontName}': {ex.Message}");
            return;
        }

        if (string.IsNullOrWhiteSpace(source))
        {
            return;
        }

        try
        {
            var bytes = ByteDataSource.GetBytesAsync(source).GetAwaiter().GetResult();
            RegisterFontData(bytes);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Could not load font '{fontName}' from '{source}': {ex.Message}");
        }
    }

    private static string CreateFontWidth(this string fontName, ref Style textStyle)
    {
        fontName = fontName.CheckForFontWidth(ref textStyle, SKFontStyleWidth.UltraCondensed)
            .CheckForFontWidth(ref textStyle, SKFontStyleWidth.ExtraCondensed)
            .CheckForFontWidth(ref textStyle, SKFontStyleWidth.Condensed)
            .CheckForFontWidth(ref textStyle, SKFontStyleWidth.SemiCondensed)
            .CheckForFontWidth(ref textStyle, SKFontStyleWidth.Normal)
            .CheckForFontWidth(ref textStyle, SKFontStyleWidth.SemiExpanded)
            .CheckForFontWidth(ref textStyle, SKFontStyleWidth.Expanded)
            .CheckForFontWidth(ref textStyle, SKFontStyleWidth.ExtraExpanded)
            .CheckForFontWidth(ref textStyle, SKFontStyleWidth.UltraExpanded);

        return fontName.Replace("  ", " ").Trim();
    }

    private static string CheckForFontWidth(this string fontName, ref Style textStyle, SKFontStyleWidth width)
    {
        if (fontName.Contains(width.ToString(), System.StringComparison.InvariantCultureIgnoreCase))
        {
            textStyle.FontWidth = width;
            fontName = fontName.Replace(width.ToString(), "", System.StringComparison.InvariantCultureIgnoreCase);
        }

        return fontName;
    }

    private static string CreateFontWeight(this string fontName, ref Style textStyle)
    {
        fontName = fontName.CheckForFontWeight(ref textStyle, "regular", 400)
            .CheckForFontWeight(ref textStyle, "invisible", 0)
            .CheckForFontWeight(ref textStyle, "thin", 100)
            .CheckForFontWeight(ref textStyle, "extralight", 200)
            .CheckForFontWeight(ref textStyle, "light", 300)
            .CheckForFontWeight(ref textStyle, "normal", 400)
            .CheckForFontWeight(ref textStyle, "medium", 500)
            .CheckForFontWeight(ref textStyle, "semibold", 600)
            .CheckForFontWeight(ref textStyle, "bold", 700)
            .CheckForFontWeight(ref textStyle, "textrabold", 800)
            .CheckForFontWeight(ref textStyle, "black", 900)
            .CheckForFontWeight(ref textStyle, "extrablack", 1000);

        return fontName.Replace("  ", " ").Trim();
    }

    private static string CheckForFontWeight(this string fontName, ref Style textStyle, string name, int weight)
    {
        if (fontName.Contains(name, System.StringComparison.InvariantCultureIgnoreCase))
        {
            textStyle.FontWeight = weight;
            fontName = fontName.Replace(name, "", System.StringComparison.InvariantCultureIgnoreCase);
        }

        return fontName;
    }

    private static string CreateFontItalic(this string fontName, ref Style textStyle)
    {
        if (fontName.Contains("italic", System.StringComparison.InvariantCultureIgnoreCase))
        {
            textStyle.FontItalic = true;
            fontName = fontName.Replace("italic", "", System.StringComparison.InvariantCultureIgnoreCase);
        }

        return fontName.Replace("  ", " ").Trim();
    }
}
