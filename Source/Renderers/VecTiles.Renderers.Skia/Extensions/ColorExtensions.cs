using SkiaSharp;
using VecTiles.Common.Primitives;

namespace VecTiles.Renderers.Skia.Extensions;

public static class ColorExtensions
{
    public static SKColor ToSKColor(this Color color)
    {
        return new SKColor(color.R, color.G, color.B, color.A);
    }
}
