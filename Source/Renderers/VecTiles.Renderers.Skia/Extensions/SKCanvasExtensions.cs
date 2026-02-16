using SkiaSharp;

namespace VecTiles.Renderers.Skia.Extensions;

public static class SkiaTextPathExtensions
{
    public enum PathTextAlign
    {
        Left,
        Center,
        Right
    }
    
    public static void DrawTextOnPath(
            this SKCanvas canvas,
            string text,
            SKPath path,
            SKPaint paint,
            PathTextAlign align = PathTextAlign.Left,
            float startOffset = 0,
            float offsetFromPath = 0,
            bool isClosed = false)
    {
        if (string.IsNullOrEmpty(text))
            return;

        using var measure = new SKPathMeasure(path, isClosed);

        float pathLength = measure.Length;
        float textWidth = paint.MeasureText(text);

        // Alignment berücksichtigen
        float distance = startOffset;

        switch (align)
        {
            case PathTextAlign.Center:
                distance += (pathLength - textWidth) / 2f;
                break;

            case PathTextAlign.Right:
                distance += pathLength - textWidth;
                break;
        }

        foreach (char c in text)
        {
            string s = c.ToString();
            float charWidth = paint.MeasureText(s);

            float charCenter = distance + charWidth / 2f;

            if (charCenter > pathLength)
                break;

            if (measure.GetPositionAndTangent(
                    charCenter,
                    out SKPoint position,
                    out SKPoint tangent))
            {
                float angle = (float) Math.Atan2(tangent.Y, tangent.X);

                canvas.Save();

                // Positionieren
                canvas.Translate(position);

                // Rotieren entlang Tangente
                canvas.RotateRadians(angle);

                // Abstand vom Pfad (normal zur Tangente)
                canvas.Translate(0, -offsetFromPath);

                // Baseline berücksichtigen
                var metrics = paint.FontMetrics;
                float baselineOffset = -metrics.Ascent;

                canvas.DrawText(s, -charWidth / 2f, baselineOffset, paint);

                canvas.Restore();
            }

            distance += charWidth;
        }
    }
}
