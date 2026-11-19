using System.Collections.Concurrent;
using Topten.RichTextKit;

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
        // TODO: Activate text letter spacing
        // Can be activated when we switch to RichTextKit >= 0.6.x (Style.LetterSpacing exists from that version on)
        //float LetterSpacing,
        float? MaxWidth,
        TextAlignment Alignment,
        int Direction);

    /// <summary>
    /// Gets a cached <see cref="RenderedText"/> for the given text and layout
    /// parameters, or creates and measures a new one. The created text block is
    /// shared between all callers with identical parameters, so it must not be
    /// modified afterwards.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="styleName">Name of the style to resolve fonts for.</param>
    /// <param name="fontNames">Candidate font names.</param>
    /// <param name="fontSize">Font size in pixels.</param>
    /// <param name="lineHeight">Line height as multiple of the font size.</param>
    /// <param name="maxWidth">Maximum width of the text block, or null.</param>
    /// <param name="alignment">Alignment of the text block.</param>
    /// <param name="direction">Text direction of the symbol.</param>
    /// <returns>A measured <see cref="RenderedText"/>.</returns>
    public static RenderedText GetOrCreate(string text, string styleName, string[] fontNames, float fontSize,
        float lineHeight,
        // TODO: Activate text letter spacing
        // Can be activated when we switch to RichTextKit >= 0.6.x (Style.LetterSpacing exists from that version on)
        //float letterSpacing,
        float? maxWidth, TextAlignment alignment, int direction)
    {
        var key = new TextLayoutKey(text, styleName, string.Join('\0', fontNames), fontSize, lineHeight, maxWidth,
            alignment, direction);
        // TODO: Activate text letter spacing
        // Can be activated when we switch to RichTextKit >= 0.6.x (Style.LetterSpacing exists from that version on)
        //var key = new TextLayoutKey(text, styleName, string.Join('\0', fontNames), fontSize, lineHeight,
        //    letterSpacing, maxWidth, alignment, direction);

        if (Cache.TryGetValue(key, out var renderedText))
        {
            return renderedText;
        }

        renderedText = LayoutRenderedText(text, styleName, fontNames, fontSize, lineHeight, maxWidth, alignment);
        // TODO: Activate text letter spacing
        // Can be activated when we switch to RichTextKit >= 0.6.x (Style.LetterSpacing exists from that version on)
        //renderedText = LayoutRenderedText(text, styleName, fontNames, fontSize, lineHeight, letterSpacing, maxWidth, alignment);

        Cache[key] = renderedText;

        if (Cache.Count > MaxEntries)
        {
            Cache.Clear();
        }

        return renderedText;
    }

    private static RenderedText LayoutRenderedText(string text, string styleName, string[] fontNames,
        float fontSize, float lineHeight,
        // TODO: Activate text letter spacing
        // Can be activated when we switch to RichTextKit >= 0.6.x (Style.LetterSpacing exists from that version on)
        //float letterSpacing,
        float? maxWidth, TextAlignment alignment)
    {
        var result = new RenderedText(text, FontCache.CreateFonts(styleName, fontNames));

        // Use the caching font mapper, so typefaces are resolved once per font
        // signature. The global default mapper is left untouched for other
        // users of RichTextKit (e.g. Mapsui).
        result.Text.FontMapper = CachedFontMapper.Instance;

        result.TextStyle.FontSize = fontSize;
        // TODO: Activate text letter spacing
        // Can be activated when we switch to RichTextKit >= 0.6.x (Style.LetterSpacing exists from that version on)
        //result.TextStyle.LetterSpacing = letterSpacing;

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
            result.TextStyle.LineHeight = lineHeight;
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
}
