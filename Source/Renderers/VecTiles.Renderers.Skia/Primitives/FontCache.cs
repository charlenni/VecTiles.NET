using SkiaSharp;
using Topten.RichTextKit;
using VecTiles.Common.Exceptions;

namespace VecTiles.Renderers.Skia.Primitives;

public static class FontCache
{
    private static readonly Dictionary<(string, string), Topten.RichTextKit.Style> TextStyles = new();
    private static readonly object TextStylesLock = new();

    public static Style? CreateFonts(string styleName, string[] names)
    {
        if (names == null || names.Length == 0)
        {
            throw new FontNotFoundException($"Font of style not found");
        }

        foreach (var name in names)
        {
            var textStyle = CreateFont(styleName, name);

            if (textStyle != null)
            {
                return textStyle;
            }
        }

        throw new FontNotFoundException(names.Length == 1 ? $"Font '{names[0]}' not found" : $"Fonts '{string.Join(", ", names)}' not found");
    }

    private static Topten.RichTextKit.Style? CreateFont(string styleName, string fontName)
    {
        if (string.IsNullOrEmpty(fontName))
        {
            return null;
        }

        var key = (styleName, fontName);

        lock (TextStylesLock)
        {
            if (TextStyles.TryGetValue(key, out var textStyle))
            {
                return textStyle;
            }

            textStyle = new Topten.RichTextKit.Style();

            // TODO: Create correct family name
            var fontFamilyName = fontName.CreateFontWidth(ref textStyle)
                .CreateFontWeight(ref textStyle)
                .CreateFontItalic(ref textStyle);

            var fontManager = SKFontManager.Default;
            var typeface = fontManager.MatchFamily(fontFamilyName);

            // Check, if result is the font family asked for
            bool fontExists = typeface != null && typeface.FamilyName.Equals(fontFamilyName, StringComparison.OrdinalIgnoreCase);

            // The style font is not installed (e.g. Noto Sans/Roboto on
            // Windows). Fall back to the system default family instead of
            // failing: a FontNotFoundException here would suppress every
            // label of the style. Weights/widths/italics of the parsed style
            // are kept and resolved against the fallback family.
            if (!fontExists)
            {
                fontFamilyName = SKTypeface.Default.FamilyName;
            }

            textStyle.FontFamily = fontFamilyName;

            TextStyles.Add(key, textStyle);

            return textStyle;
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