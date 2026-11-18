using SkiaSharp;

namespace VecTiles.Renderers.Skia.Extensions;

public static class SkiaTextPathExtensions
{
    /// <summary>
    /// Draws text along a path by drawing each character on its own, rotated to
    /// the tangent of the path at the character position (like MapLibre renders
    /// labels along lines). The vertical center of the characters is placed at
    /// the path, shifted by <paramref name="offsetFromPath"/>.
    /// </summary>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="text">Text to draw.</param>
    /// <param name="path">Path to draw the text on.</param>
    /// <param name="startOffset">Distance along the path where the text starts.</param>
    /// <param name="offsetFromPath">Distance between the path and the top of the characters.</param>
    /// <param name="paints">Paints used to draw each character, in drawing order
    /// (e.g. halo paint first, fill paint last).</param>
    public static void DrawTextOnPath(
            this SKCanvas canvas,
            string text,
            SKPath path,
            float startOffset,
            float offsetFromPath,
            params SKPaint[] paints)
    {
        if (string.IsNullOrEmpty(text) || paints.Length == 0)
        {
            return;
        }

        using var measure = new SKPathMeasure(path);

        float pathLength = measure.Length;
        float distance = startOffset;

        foreach (char c in text)
        {
            string s = c.ToString();
            float charWidth = paints[0].MeasureText(s);

            float charCenter = distance + charWidth / 2f;

            if (charCenter > pathLength)
            {
                break;
            }

            if (charCenter >= 0 &&
                measure.GetPositionAndTangent(charCenter, out SKPoint position, out SKPoint tangent))
            {
                float angle = (float)Math.Atan2(tangent.Y, tangent.X);

                canvas.Save();

                // Position on the path, rotated to the local tangent
                canvas.Translate(position);
                canvas.RotateRadians(angle);

                // Gap to the path; the baseline is raised by the ascent, so that
                // the vertical center of the character sits at this offset
                canvas.Translate(0, -offsetFromPath);

                foreach (var paint in paints)
                {
                    canvas.DrawText(s, -charWidth / 2f, -paint.FontMetrics.Ascent, paint);
                }

                canvas.Restore();
            }

            distance += charWidth;
        }
    }
}
