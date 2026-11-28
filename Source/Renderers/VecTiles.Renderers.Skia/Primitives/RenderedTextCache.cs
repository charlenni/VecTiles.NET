using SkiaSharp;
using System.Collections.Concurrent;
using Topten.RichTextKit;
using VecTiles.Common.Exceptions;

namespace VecTiles.Renderers.Skia.Primitives;

/// <summary>
/// Cache for <see cref="RenderedText"/> instances.
///
/// Laying out a <see cref="Topten.RichTextKit.TextBlock"/> is expensive (a full text
/// shaping run of several milliseconds). Texts with identical content and layout
/// parameters always produce the same layout, so they share one measured instance.
/// This avoids repeated layout work for repeated labels and for symbol sets that
/// are rebuilt while the tile set changes.
/// </summary>
public static class RenderedTextCache
{
    /// <summary>
    /// Maximum number of cached entries. When exceeded, the cache is cleared
    /// entirely to limit memory usage.
    /// </summary>
    private const int MaxEntries = 4096;

    private static readonly ConcurrentDictionary<TextLayoutKey, RenderedText> Cache = new();

    private readonly record struct TextLayoutKey(
        string Text,
        string StyleName,
        string FontNames,
        float FontSize,
        float LineHeight,
        float LetterSpacing,
        float? MaxWidth,
        TextAlignment Alignment,
        int Direction);

    /// <summary>
    /// Gets a cached <see cref="RenderedText"/> for the given text and layout
    /// parameters, or creates and measures a new one. The created text block is
    /// shared between all callers with identical parameters. Its style is owned
    /// by the entry: the renderers may adjust paint attributes (colors, halo)
    /// per draw, but layout attributes (font size, letter spacing, line height)
    /// must not be modified, so the shared layout stays valid.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="styleName">Name of the style to resolve fonts for.</param>
    /// <param name="fontNames">Candidate font names.</param>
    /// <param name="fontSize">Font size in pixels.</param>
    /// <param name="lineHeight">Line height as multiple of the font size.</param>
    /// <param name="letterSpacing">Additional spacing between characters.</param>
    /// <param name="maxWidth">Maximum width of the text block, or null.</param>
    /// <param name="alignment">Alignment of the text block.</param>
    /// <param name="direction">Text direction of the symbol.</param>
    /// <returns>A measured <see cref="RenderedText"/>.</returns>
    public static RenderedText GetOrCreate(string text, string styleName, string[] fontNames, float fontSize,
        float lineHeight, float letterSpacing, float? maxWidth, TextAlignment alignment, int direction)
    {
        var key = new TextLayoutKey(text, styleName, string.Join('\0', fontNames), fontSize, lineHeight,
            letterSpacing, maxWidth, alignment, direction);

        if (Cache.TryGetValue(key, out var renderedText))
        {
            return renderedText;
        }

        renderedText = LayoutRenderedText(text, styleName, fontNames, fontSize, lineHeight, letterSpacing, maxWidth, alignment);

        Cache[key] = renderedText;

        if (Cache.Count > MaxEntries)
        {
            Cache.Clear();
        }

        return renderedText;
    }

    private static RenderedText LayoutRenderedText(string text, string styleName, string[] fontNames,
        float fontSize, float lineHeight, float letterSpacing, float? maxWidth, TextAlignment alignment)
    {
        // FontCache returns a shared style instance per font signature; this
        // entry owns a private copy, because the layout parameters below are
        // entry specific and RichTextKit reads the live style at first paint
        var textStyle = FontCache.CreateFonts(styleName, fontNames)?.Modify()
            ?? throw new FontNotFoundException($"Font of style '{styleName}' not found");

        // Like MapLibre, a word wider than the maximum width must overflow on
        // its own line instead of being broken mid-word. RichTextKit breaks
        // such a word, so in that case the line breaks are determined here,
        // inserted as newlines and the max width is dropped, so the text block
        // cannot break words. Texts without an overflowing word keep the usual
        // RichTextKit layout.
        if (maxWidth is { } max && HasWordWiderThan(text, textStyle, fontSize, letterSpacing, max))
        {
            text = WrapBetweenWords(text, textStyle, fontSize, letterSpacing, max);
            maxWidth = null;
        }

        var result = new RenderedText(text, textStyle);

        // Use the caching font mapper, so typefaces are resolved once per font
        // signature. The global default mapper is left untouched for other
        // users of RichTextKit (e.g. Mapsui).
        result.Text.FontMapper = CachedFontMapper.Instance;

        result.TextStyle.FontSize = fontSize;
        result.TextStyle.LetterSpacing = letterSpacing;

        result.Text.Alignment = alignment;
        result.Text.MaxWidth = maxWidth;
        result.Text.ApplyStyle(0, result.Text.Length, result.TextStyle);

        // MeasuredWidth and MeasuredHeight trigger the layout and require significant
        // CPU cycles, so cache them for later use
        result.MeasuredWidth = result.Text.MeasuredWidth;
        result.MeasuredHeight = result.Text.MeasuredHeight;

        // Line height only changes the spacing between wrapped lines, so single-line
        // texts keep the compact default layout
        if (result.Text.LineCount > 1)
        {
            result.TextStyle.LineHeight = RichTextLineHeight(lineHeight, fontSize, result.TextStyle);
            result.Text.ApplyStyle(0, result.Text.Length, result.TextStyle);
            result.MeasuredWidth = result.Text.MeasuredWidth;
            result.MeasuredHeight = result.Text.MeasuredHeight;
        }

        // Keep MeasuredPadding.Left as drawing correction. RichTextKit positions lines
        // including the left padding caused by the maximum width; Paint then draws at
        // the padded position, which the renderers shift back by LeftRightCorrection.
        // No second layout pass is needed for this: single-line metrics are identical
        // with and without MaxWidth.
        result.LeftRightCorrection = result.Text.MeasuredPadding.Left;

        return result;
    }

    /// <summary>
    /// Converts a line height given as a multiple of the font size (the
    /// MapLibre text-line-height semantics, where the distance between the
    /// baselines of two lines is line height times font size) into the
    /// multiplier RichTextKit expects, which scales the natural line height
    /// of the font (ascent plus descent) instead of the font size. Falls
    /// back to the raw value if the typeface cannot be resolved.
    /// </summary>
    private static float RichTextLineHeight(float lineHeight, float fontSize, Style textStyle)
    {
        // The typeface is shared via the font mapper cache and must not be
        // disposed here ( disposing it crashes the next layout that uses it)
        var typeface = CachedFontMapper.Instance.TypefaceFromStyle(textStyle, ignoreFontVariants: false);

        if (typeface is null || fontSize <= 0)
        {
            return lineHeight;
        }

        using var font = new SKFont(typeface, fontSize);
        var naturalLineHeight = font.Spacing;

        return naturalLineHeight > 0 ? lineHeight * fontSize / naturalLineHeight : lineHeight;
    }

    /// <summary>
    /// Checks whether any word of the text (newlines not counted) is wider than
    /// the maximum width, using the same measurement as
    /// <see cref="WrapBetweenWords"/>. Returns false if the font cannot be
    /// resolved for measurement.
    /// </summary>
    private static bool HasWordWiderThan(string text, Style textStyle, float fontSize, float letterSpacing,
        float maxWidth)
    {
        // The typeface is shared via the font mapper cache and must not be
        // disposed here (disposing it crashes the next layout that uses it)
        var typeface = CachedFontMapper.Instance.TypefaceFromStyle(textStyle, ignoreFontVariants: false);

        if (typeface is null)
        {
            return false;
        }

        using var font = new SKFont(typeface, fontSize);

        foreach (var segment in text.Split('\n'))
        {
            foreach (var word in segment.Split(' '))
            {
                if (font.MeasureText(word) + letterSpacing * (word.Length - 1) > maxWidth)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Wraps the text at spaces so that no line exceeds the maximum width,
    /// keeping existing newlines. A word wider than the maximum width starts
    /// (or occupies) a line unbroken, like MapLibre does; spaces at the break
    /// points are dropped. Returns the text unchanged if the font cannot be
    /// resolved for measurement.
    /// </summary>
    private static string WrapBetweenWords(string text, Style textStyle, float fontSize, float letterSpacing,
        float maxWidth)
    {
        // The typeface is shared via the font mapper cache and must not be
        // disposed here (disposing it crashes the next layout that uses it)
        var typeface = CachedFontMapper.Instance.TypefaceFromStyle(textStyle, ignoreFontVariants: false);

        if (typeface is null)
        {
            return text;
        }

        using var font = new SKFont(typeface, fontSize);

        float Measure(string value) => font.MeasureText(value) + letterSpacing * (value.Length - 1);

        var lines = new List<string>();

        foreach (var segment in text.Split('\n'))
        {
            var current = string.Empty;

            foreach (var word in segment.Split(' '))
            {
                if (current.Length == 0)
                {
                    current = word;
                }
                else
                {
                    var candidate = current + ' ' + word;

                    if (Measure(candidate) <= maxWidth)
                    {
                        current = candidate;
                    }
                    else
                    {
                        lines.Add(current);
                        current = word;
                    }
                }
            }

            lines.Add(current.TrimEnd());
        }

        return string.Join('\n', lines);
    }
}
