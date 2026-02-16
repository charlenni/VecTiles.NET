using SkiaSharp;

namespace VecTiles.Renderers.Skia.Extensions;

public static class SKPathExtensions
{
        /// <summary>
    /// Checks if the text fits on the next linear segment of the path
    /// starting from startDistance along the current SKPathMeasure contour.
    /// Assumes the path consists of straight lines only.
    /// </summary>
    public static bool CanFitOnNextLineSegment(
        this SKPath path,
        float startDistance,
        float requiredLength,
        float step = 1f) // step for distance sampling
    {
        if (requiredLength <= 0)
            return false;

        float accumulated = 0f;
        SKPoint lastPoint = new SKPoint();

        bool firstMove = true;

        using (var iter = path.CreateRawIterator())
        {
            SKPathVerb verb;
            SKPoint[] points = new SKPoint[4];

            while ((verb = iter.Next(points)) != SKPathVerb.Done)
            {
                switch (verb)
                {
                    case SKPathVerb.Move:
                        lastPoint = points[0];
                        if (firstMove)
                            firstMove = false;
                        break;

                    case SKPathVerb.Line:
                        SKPoint start = lastPoint;
                        SKPoint end = points[1];
                        float segmentLength = Distance(start, end);

                        if (accumulated + segmentLength > startDistance)
                        {
                            // startDistance falls on this segment
                            float segmentOffset = startDistance - accumulated;
                            float remaining = segmentLength - segmentOffset;
                            return remaining >= requiredLength;
                        }

                        accumulated += segmentLength;
                        lastPoint = end;
                        break;

                    case SKPathVerb.Quad:
                    case SKPathVerb.Cubic:
                    case SKPathVerb.Close:
                        // ignore, only lines are supported
                        break;
                }
            }
        }

        return false; // startDistance beyond path
    }

    private static float Distance(SKPoint a, SKPoint b)
    {
        float dx = b.X - a.X;
        float dy = b.Y - a.Y;
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }
}