using SkiaSharp;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace VecTiles.Renderers.Skia.Test;

public static class Utilities
{
    public static SKBitmap CreateBitmap(int width, int height)
    {
        var imageInfo = new SKImageInfo 
        { 
            Width = width, 
            Height = height, 
            ColorType = SKColorType.Rgba8888, 
            AlphaType = SKAlphaType.Premul 
        };

        return new SKBitmap(imageInfo);
    }

    public static void SaveBitmap(SKBitmap bitmap, string filename)
    {
        var directory = Path.GetDirectoryName(filename);

        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using (var image = SKImage.FromBitmap(bitmap))
        using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
        using (var stream = File.OpenWrite(filename))
        {
            data.SaveTo(stream);
        }
    }

    public static void SaveAndDestroyBitmap(SKBitmap bitmap, string filename)
    {
        SaveBitmap(bitmap, filename);

        bitmap.Dispose();
    }

    /// <summary>
    /// Compare two bitmap, if they have (nearly) same content
    /// </summary>
    /// <param name="bitmap">Bitmap to compare</param>
    /// <param name="original">Original bitmap</param>
    /// <returns>True, if the images are (nearly) identical</returns>
    public static bool CompareBitmaps(string bitmap, string original)
    {
        using (var streamBitmap = File.OpenRead(bitmap))
        using (var streamOriginal = File.OpenRead(original))
        {
            return Compare(streamBitmap, streamOriginal);
        }
    }

    /// <summary>
    /// Compares to bitmap stream, if they have (nearly) the same content.
    /// </summary>
    /// <remarks>
    /// Copied from Mapsui. License (https://github.com/Mapsui/Mapsui/blob/main/LICENSE) see there.
    /// </remarks>
    /// <param name="bitmapStream1">First bitmap stream</param>
    /// <param name="bitmapStream2">Second bitmap stream</param>
    /// <param name="allowedColorDistance">Difference in color values</param>
    /// <param name="proportionCorrect"></param>
    /// <returns>True, if the bitmaps are the same inside given range</returns>
    private static bool Compare(Stream? bitmapStream1, Stream? bitmapStream2, int allowedColorDistance = 0, double proportionCorrect = 1)
    {
        // The bitmaps in WPF can slightly differ from test to test. No idea why. So introduced proportion correct.

        long trueCount = 0;
        long falseCount = 0;

        if (bitmapStream1 == null && bitmapStream2 == null)
        {
            return true;
        }

        if (bitmapStream1 == null || bitmapStream2 == null)
        {
            return false;
        }

        bitmapStream1.Position = 0;
        bitmapStream2.Position = 0;

        using var skData1 = SKData.Create(bitmapStream1);
        var bitmap1 = SKBitmap.FromImage(SKImage.FromEncodedData(skData1));
        using var skData2 = SKData.Create(bitmapStream2);
        var bitmap2 = SKBitmap.FromImage(SKImage.FromEncodedData(skData2));

        if (bitmap1.Width != bitmap2.Width || bitmap1.Height != bitmap2.Height)
        {
            return false;
        }

        for (var x = 0; x < bitmap1.Width; x++)
        {
            for (var y = 0; y < bitmap1.Height; y++)
            {
                var color1 = bitmap1.GetPixel(x, y);
                var color2 = bitmap2.GetPixel(x, y);
                if (color1 == color2)
                    trueCount++;
                else
                {
                    if (CompareColors(color1, color2, allowedColorDistance))
                        trueCount++;
                    else
                        falseCount++;
                }
            }
        }

        var proportion = (double)(trueCount) / (trueCount + falseCount);
        return proportionCorrect <= proportion;
    }

    private static bool CompareColors(SKColor color1, SKColor color2, int allowedColorDistance)
    {
        if (color1.Alpha == 0 && color2.Alpha == 0) return true; // If both are transparent all colors are ignored
        if (Math.Abs(color1.Alpha - color2.Alpha) > allowedColorDistance) return false;
        if (Math.Abs(color1.Red - color2.Red) > allowedColorDistance) return false;
        if (Math.Abs(color1.Green - color2.Green) > allowedColorDistance) return false;
        if (Math.Abs(color1.Blue - color2.Blue) > allowedColorDistance) return false;
        return true;
    }
}
