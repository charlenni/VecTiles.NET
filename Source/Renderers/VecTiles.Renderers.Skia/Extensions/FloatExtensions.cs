using SkiaSharp;

namespace VecTiles.Renderers.Skia.Extensions;

public static class FloatExtensions
{
    public static SKPoint ToPoint(this float[] offset, float scale = 1.0f)
    {
        if (offset.Length == 2)
        {
            return new SKPoint(offset[0] * scale, offset[1] * scale);
        }
        else
        {
            return new SKPoint(0, 0);
        }
    }

}
