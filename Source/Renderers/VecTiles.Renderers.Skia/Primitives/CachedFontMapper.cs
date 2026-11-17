using System.Collections.Concurrent;
using SkiaSharp;
using Topten.RichTextKit;

namespace VecTiles.Renderers.Skia.Primitives;

/// <summary>
/// Font mapper that caches resolved <see cref="SKTypeface"/> instances.
///
/// RichTextKit resolves the typeface of every style run with
/// <see cref="SKTypeface.FromFamilyName"/> during layout, which queries the font
/// system and takes several milliseconds per call. This mapper caches typefaces by
/// their font signature, so the lookup only happens once per font signature
/// instead of once per layout run.
///
/// The instance is assigned per text block via <see cref="TextBlock.FontMapper"/>;
/// the global <see cref="FontMapper.Default"/> is not replaced, so it stays
/// effective for other users of RichTextKit (e.g. Mapsui).
/// </summary>
public class CachedFontMapper : FontMapper
{
    /// <summary>
    /// Shared instance of this mapper to be assigned to text blocks.
    /// </summary>
    public static readonly CachedFontMapper Instance = new();

    private CachedFontMapper()
    {
    }

    private readonly ConcurrentDictionary<FontKey, SKTypeface> _cache = new();

    private readonly record struct FontKey(
        string? FontFamily,
        int FontWeight,
        SKFontStyleWidth FontWidth,
        bool FontItalic,
        FontVariant FontVariant,
        bool IgnoreFontVariants);

    /// <inheritdoc />
    public override SKTypeface TypefaceFromStyle(IStyle style, bool ignoreFontVariants)
    {
        var key = new FontKey(style.FontFamily, style.FontWeight, style.FontWidth, style.FontItalic,
            style.FontVariant, ignoreFontVariants);

        return _cache.GetOrAdd(key, _ => base.TypefaceFromStyle(style, ignoreFontVariants));
    }
}
