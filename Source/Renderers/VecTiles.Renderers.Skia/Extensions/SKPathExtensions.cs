using SkiaSharp;

namespace VecTiles.Renderers.Skia.Extensions;

public static class SKPathExtensions
{
    /// <summary>
    /// Creates a copy of the path with reversed direction. Only supports paths
    /// consisting of MoveTo / LineTo verbs, which is what the line symbol
    /// renderers build from the geometries.
    /// </summary>
    public static SKPath Reverse(this SKPath path)
    {
        var result = new SKPath();
        var points = new List<SKPoint>();

        using var iter = path.CreateRawIterator();
        SKPoint[] pointsBuffer = new SKPoint[4];

        SKPathVerb verb;

        while ((verb = iter.Next(pointsBuffer)) != SKPathVerb.Done)
        {
            switch (verb)
            {
                case SKPathVerb.Move:
                    AddReversedPolygon(result, points);
                    points.Clear();
                    points.Add(pointsBuffer[0]);
                    break;

                case SKPathVerb.Line:
                    points.Add(pointsBuffer[1]);
                    break;
            }
        }

        AddReversedPolygon(result, points);

        return result;
    }

    private static void AddReversedPolygon(SKPath result, List<SKPoint> points)
    {
        if (points.Count < 2)
        {
            return;
        }

        for (var i = points.Count - 1; i >= 0; i--)
        {
            if (i == points.Count - 1)
            {
                result.MoveTo(points[i]);
            }
            else
            {
                result.LineTo(points[i]);
            }
        }
    }
}
