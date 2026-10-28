using NetTopologySuite.Geometries;

namespace VecTiles.Styles.OpenMapTiles.Extensions;

public static class EnvelopeExtensions
{
    const double convert = Math.PI / 180.0;

    public static void RotateDegrees(this Envelope envelope, double angle)
    {
        envelope.RotateRadiant(angle * convert);
    }

    public static void RotateRadiant(this Envelope envelope, double angle)
    {
        var sin = Math.Abs(Math.Sin(angle));
        var cos = Math.Abs(Math.Cos(angle));

        var newHalfWidth = (envelope.Width * cos + envelope.Height * sin) / 2.0;
        var newHalfHeight = (envelope.Width * sin + envelope.Height * cos) / 2.0;
        var center = envelope.Centre;

        envelope.Init(center.X - newHalfWidth, center.X + newHalfWidth, center.Y - newHalfHeight, center.Y + newHalfHeight);

            /*
        var minX = envelope.MinX;
        var maxX = envelope.MaxX;
        var minY = envelope.MinY;
        var maxY = envelope.MaxY;
        var sin = Math.Sin(angle);
        var cos = Math.Cos(angle);

        var x1 = minX * cos - minY * sin;
        var y1 = minX * sin + minY * cos;
        var x2 = maxX * cos - minY * sin;
        var y2 = maxX * sin + minY * cos;
        var x3 = maxX * cos - maxY * sin;
        var y3 = maxX * sin + maxY * cos;
        var x4 = minX * cos - maxY * sin;
        var y4 = minX * sin + maxY * cos;

        minX = Math.Min(x1, Math.Min(x2, Math.Min(x3, x4)));
        minY = Math.Min(y1, Math.Min(y2, Math.Min(y3, y4)));
        maxX = Math.Max(x1, Math.Max(x2, Math.Max(x3, x4)));
        maxY = Math.Max(y1, Math.Max(y2, Math.Max(y3, y4)));

        envelope.Init(minX, maxX, minY, maxY);*/
    }

    public static void RotateDegreesAroundAnchor(this Envelope envelope, double angleDegrees, double anchorX, double anchorY)
    {
        // Convert angle from degrees to radians
        var angleRadians = angleDegrees * Math.PI / 180.0;

        envelope.RotateRadiantAroundAnchor(angleRadians, anchorX, anchorY);
    }

    public static void RotateRadiantAroundAnchor(this Envelope envelope, double angleRadians, double anchorX, double anchorY)
    {
        // Calculate the center of rotation in world coordinates
        var centerX = envelope.MinX + anchorX * envelope.Width;
        var centerY = envelope.MinY + anchorY * envelope.Height;

        // Translate the envelope so that the center of rotation is at the origin
        var translatedMinX = envelope.MinX - centerX;
        var translatedMinY = envelope.MinY - centerY;
        var translatedMaxX = envelope.MaxX - centerX;
        var translatedMaxY = envelope.MaxY - centerY;

        // Apply rotation
        var cosTheta = Math.Cos(angleRadians);
        var sinTheta = Math.Sin(angleRadians);

        // Rotate each corner of the envelope
        var rotatedCornersX = new double[4];
        var rotatedCornersY = new double[4];

        // Rotate the four corners of the envelope
        rotatedCornersX[0] = translatedMinX * cosTheta - translatedMinY * sinTheta;
        rotatedCornersY[0] = translatedMinX * sinTheta + translatedMinY * cosTheta;

        rotatedCornersX[1] = translatedMaxX * cosTheta - translatedMinY * sinTheta;
        rotatedCornersY[1] = translatedMaxX * sinTheta + translatedMinY * cosTheta;

        rotatedCornersX[2] = translatedMaxX * cosTheta - translatedMaxY * sinTheta;
        rotatedCornersY[2] = translatedMaxX * sinTheta + translatedMaxY * cosTheta;

        rotatedCornersX[3] = translatedMinX * cosTheta - translatedMaxY * sinTheta;
        rotatedCornersY[3] = translatedMinX * sinTheta + translatedMaxY * cosTheta;

        // Translate back
        for (var i = 0; i < 4; i++)
        {
            rotatedCornersX[i] += centerX;
            rotatedCornersY[i] += centerY;
        }

        // Find the new min and max coordinates
        var newMinX = rotatedCornersX.Min();
        var newMinY = rotatedCornersY.Min();
        var newMaxX = rotatedCornersX.Max();
        var newMaxY = rotatedCornersY.Max();

        envelope.Init(newMinX, newMaxX, newMinY, newMaxY);
    }

    // Helper method to find the minimum value in an array
    private static double Min(this double[] array)
    {
        var min = array[0];
        for (var i = 1; i < array.Length; i++)
        {
            if (array[i] < min)
                min = array[i];
        }
        return min;
    }

    // Helper method to find the maximum value in an array
    private static double Max(this double[] array)
    {
        var max = array[0];
        for (var i = 1; i < array.Length; i++)
        {
            if (array[i] > max)
                max = array[i];
        }
        return max;
    }
}
