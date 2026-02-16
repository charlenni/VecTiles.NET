using SkiaSharp;
using Topten.RichTextKit;
using VecTiles.Common.Exceptions;

namespace VecTiles.Renderers.Skia.Primitives;

public static class FontCache
{
    private static readonly Dictionary<(string, string), Topten.RichTextKit.Style> TextStyles = new();

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

        if (TextStyles.TryGetValue((styleName, fontName), out var textStyle))
        {
            return textStyle;
        }

        textStyle = new Topten.RichTextKit.Style();

        // TODO: Create correct family name
        var fontFamilyName = fontName;

        if (fontFamilyName.Contains("condensed", System.StringComparison.InvariantCultureIgnoreCase))
        {
            textStyle.FontWidth = SKFontStyleWidth.Condensed;
            fontFamilyName = fontFamilyName.Replace("condensed", "", System.StringComparison.InvariantCultureIgnoreCase);
        }

        textStyle.FontWeight = 400;

        if (fontFamilyName.Contains("regular", System.StringComparison.InvariantCultureIgnoreCase))
        {
            textStyle.FontWeight = 400;
            fontFamilyName = fontFamilyName.Replace("regular", "", System.StringComparison.InvariantCultureIgnoreCase);
        }

        if (fontFamilyName.Contains("medium", System.StringComparison.InvariantCultureIgnoreCase))
        {
            textStyle.FontWeight = 500;
            fontFamilyName = fontFamilyName.Replace("medium", "", System.StringComparison.InvariantCultureIgnoreCase);
        }

        if (fontFamilyName.Contains("bold", System.StringComparison.InvariantCultureIgnoreCase))
        {
            textStyle.FontWeight = 500;
            fontFamilyName = fontFamilyName.Replace("bold", "", System.StringComparison.InvariantCultureIgnoreCase);
        }

        if (fontFamilyName.Contains("italic", System.StringComparison.InvariantCultureIgnoreCase))
        {
            textStyle.FontItalic = true;
            fontFamilyName = fontFamilyName.Replace("italic", "", System.StringComparison.InvariantCultureIgnoreCase);
        }

        fontFamilyName = fontFamilyName.Replace("  ", " ").Trim();

        var fontManager = SKFontManager.Default;
        var typeface = fontManager.MatchFamily(fontFamilyName);

        // Check, if result is the font family asked for
        bool fontExists = typeface != null && typeface.FamilyName.Equals(fontFamilyName, StringComparison.OrdinalIgnoreCase);

        if (!fontExists)
        {
            return null;
        }

        textStyle.FontFamily = fontFamilyName;

        TextStyles.Add((styleName, fontName), textStyle);

        return textStyle;
    }
}