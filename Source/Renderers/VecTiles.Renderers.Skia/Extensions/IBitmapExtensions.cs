using SkiaSharp;
using VecTiles.Common.Interfaces;

namespace VecTiles.Renderers.Skia.Extensions;

public static class IBitmapExtensions
{
    public static SKImage ToSKImage(this IBitmap bitmap)
    {
        bitmap.Native ??= SKImage.FromEncodedData(bitmap.Binary);

        return ((SKImage) bitmap.Native);
    }
}