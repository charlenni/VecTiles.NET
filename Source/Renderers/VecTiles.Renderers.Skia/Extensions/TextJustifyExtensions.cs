using Topten.RichTextKit;
using VecTiles.Common.Enums;

namespace VecTiles.Renderers.Skia.Extensions;

public static class TextJustifyExtensions
{
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