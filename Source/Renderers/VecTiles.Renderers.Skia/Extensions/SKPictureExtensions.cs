using SkiaSharp;

namespace VecTiles.Renderers.Skia.Extensions
{
    public static class SKPictureExtensions
    {
        public static SKData ToPng(this SKPicture picture, int quality = 100)
        {
            var bitmap = new SKBitmap((int)picture.CullRect.Width, (int)picture.CullRect.Height, SKColorType.Rgba8888, SKAlphaType.Opaque);
            var canvas = new SKCanvas(bitmap);

            canvas.DrawPicture(picture);

            return bitmap.Encode(SKEncodedImageFormat.Png, quality);
        }

        public static SKData ToJpg(this SKPicture picture, int quality = 100)
        {
            var bitmap = new SKBitmap((int)picture.CullRect.Width, (int)picture.CullRect.Height, SKColorType.Rgba8888, SKAlphaType.Opaque);
            var canvas = new SKCanvas(bitmap);

            canvas.DrawPicture(picture);

            return bitmap.Encode(SKEncodedImageFormat.Jpeg, quality);
        }

        public static void ToSvg(this SKPicture picture, Stream stream)
        {
            var canvas = SKSvgCanvas.Create(picture.CullRect, stream);

            canvas.DrawPicture(picture);

            canvas.Dispose();
        }
    }
}
