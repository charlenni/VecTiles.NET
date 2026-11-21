using Topten.RichTextKit;
using VecTiles.Common.Enums;

namespace VecTiles.Renderers.Skia.Extensions;

/// <summary>
/// Extension methods to convert <see cref="TextJustify" /> values to
/// RichTextKit alignments.
/// </summary>
public static class TextJustifyExtensions
{
    /// <summary>
    /// Converts the justify value to the equivalent
    /// <see cref="TextAlignment" />. Unknown values map to
    /// <see cref="TextAlignment.Center" />.
    /// </summary>
    /// <param name="textJustify">Justify value to convert.</param>
    /// <returns>The equivalent text alignment.</returns>
    public static TextAlignment ToTextAlignment(this TextJustify textJustify)
    {
        return textJustify switch
        {
            TextJustify.Auto => TextAlignment.Auto,
            TextJustify.Center => TextAlignment.Center,
            TextJustify.Left => TextAlignment.Left,
            TextJustify.Right => TextAlignment.Right,
            _ => TextAlignment.Center
        };
    }
}