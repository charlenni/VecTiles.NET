using NetTopologySuite.Geometries;

namespace VecTiles.Styles.Mapbox.Extensions;

public static class EnvelopeExtensions
{
    const double convert = Math.PI / 180.0;

    public static void RotateDegrees(this Envelope envelope, double angle)
    {
        envelope.RotateRadiant(angle * convert);
    }

    public static void RotateRadiant(this Envelope envelope, double angle)
    {
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

        envelope.Init(minX, maxX, minY, maxY);
    }
}
