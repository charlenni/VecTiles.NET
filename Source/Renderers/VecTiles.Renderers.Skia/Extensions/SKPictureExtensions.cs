using SkiaSharp;

namespace VecTiles.Renderers.Skia.Extensions
{
    /// <summary>
    /// Extension methods to export an <see cref="SKPicture" /> to common image formats.
    /// </summary>
    public static class SKPictureExtensions
    {
        /// <summary>
        /// Draws the picture onto a bitmap and encodes it as PNG.
        /// </summary>
        /// <param name="picture">Picture to draw.</param>
        /// <param name="quality">Encoding quality.</param>
        /// <returns>The encoded PNG data.</returns>
        public static SKData ToPng(this SKPicture picture, int quality = 100)
        {
            var bitmap = new SKBitmap((int)picture.CullRect.Width, (int)picture.CullRect.Height, SKColorType.Rgba8888, SKAlphaType.Opaque);
            var canvas = new SKCanvas(bitmap);

            canvas.DrawPicture(picture);

            return bitmap.Encode(SKEncodedImageFormat.Png, quality);
        }

        /// <summary>
        /// Draws the picture onto a bitmap and encodes it as JPEG.
        /// </summary>
        /// <param name="picture">Picture to draw.</param>
        /// <param name="quality">Encoding quality.</param>
        /// <returns>The encoded JPEG data.</returns>
        public static SKData ToJpg(this SKPicture picture, int quality = 100)
        {
            var bitmap = new SKBitmap((int)picture.CullRect.Width, (int)picture.CullRect.Height, SKColorType.Rgba8888, SKAlphaType.Opaque);
            var canvas = new SKCanvas(bitmap);

            canvas.DrawPicture(picture);

            return bitmap.Encode(SKEncodedImageFormat.Jpeg, quality);
        }

        /// <summary>
        /// Draws the picture onto an SVG canvas and writes the SVG
        /// document to the given stream.
        /// </summary>
        /// <param name="picture">Picture to draw.</param>
        /// <param name="stream">Stream the SVG document is written to.</param>
        public static void ToSvg(this SKPicture picture, Stream stream)
        {
            var canvas = SKSvgCanvas.Create(picture.CullRect, stream);

            canvas.DrawPicture(picture);

            canvas.Dispose();
        }
    }
}
