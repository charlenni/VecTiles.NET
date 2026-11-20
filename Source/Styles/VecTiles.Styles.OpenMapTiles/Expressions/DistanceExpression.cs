using System.Text;
using System.Text.Json.Nodes;
using NetTopologySuite.Geometries;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;

namespace VecTiles.Styles.OpenMapTiles.Expressions;

/// <summary>
/// Implements the "distance" expression: ["distance", geometry]. The result is
/// the geodesic distance in meters between the geometry of the evaluated
/// feature and the given GeoJSON geometry, like MapLibre Native. The distance
/// is computed as an approximation on a local plane (degrees scaled with the
/// mean earth radius), which is sufficient for the short distances of the
/// viewport data use case.
/// </summary>
public class DistanceExpression : Expression
{
    /// <summary>Mean earth radius in meters (WGS84).</summary>
    public const double EarthRadius = 6371008.8;

    public const double MetersPerDegree = Math.PI * EarthRadius / 180.0;

    private readonly Geometry _geometry;

    private DistanceExpression(Geometry geometry)
    {
        _geometry = geometry;
    }

    public static IExpression? Parse(JsonArray array, ExpressionParser parser)
    {
        if (array.Count != 2)
        {
            parser.Error("Expected exactly one geometry argument.");
            return null;
        }

        var geometry = GeoJsonGeometry.Parse(array[1], parser);

        if (geometry == null)
            return null;

        return new DistanceExpression(geometry);
    }

    public override object? Evaluate(EvaluationContext ctx)
    {
        var featureGeometry = ctx.Feature?.Geometry;

        if (featureGeometry == null || featureGeometry.IsEmpty || _geometry.IsEmpty)
            return null;

        return DistanceMeters(featureGeometry, _geometry);
    }

    /// <summary>
    /// Minimum distance between two geometries in meters. Every point of the
    /// first geometry is measured against the segments of the second geometry
    /// and vice versa; the planar computation happens on a local plane at the
    /// latitude of the location, which compensates the longitude compression.
    /// </summary>
    internal static double DistanceMeters(Geometry left, Geometry right)
    {
        var leftSegments = Segments(left);
        var rightSegments = Segments(right);

        double minimum = double.MaxValue;

        foreach (var (point, segment) in ClosestPairs(left, leftSegments, right, rightSegments))
            minimum = Math.Min(minimum, PointSegmentDistance(point, segment));

        return minimum;
    }

    private static IEnumerable<(Coordinate Point, (Coordinate Start, Coordinate End) Segment)> ClosestPairs(
        Geometry left, List<(Coordinate, Coordinate)> leftSegments, Geometry right, List<(Coordinate, Coordinate)> rightSegments)
    {
        foreach (var point in left.Coordinates)
        {
            foreach (var segment in rightSegments)
                yield return (point, segment);
        }

        foreach (var point in right.Coordinates)
        {
            foreach (var segment in leftSegments)
                yield return (point, segment);
        }
    }

    private static List<(Coordinate Start, Coordinate End)> Segments(Geometry geometry)
    {
        var segments = new List<(Coordinate, Coordinate)>();

        for (int i = 1; i < geometry.Coordinates.Length; i++)
            segments.Add((geometry.Coordinates[i - 1], geometry.Coordinates[i]));

        return segments;
    }

    /// <summary>
    /// Distance of a point to a segment in meters on the local plane of the
    /// point's latitude.
    /// </summary>
    internal static double PointSegmentDistance(Coordinate point, (Coordinate Start, Coordinate End) segment)
    {
        var (x, y) = ToMeters(point.X, point.Y, point.Y);
        var (startX, startY) = ToMeters(segment.Start.X, segment.Start.Y, point.Y);
        var (endX, endY) = ToMeters(segment.End.X, segment.End.Y, point.Y);

        var deltaX = endX - startX;
        var deltaY = endY - startY;

        double squaredLength = deltaX * deltaX + deltaY * deltaY;

        double t = squaredLength == 0
            ? 0
            : Math.Clamp(((x - startX) * deltaX + (y - startY) * deltaY) / squaredLength, 0d, 1d);

        var projectedX = startX + t * deltaX - x;
        var projectedY = startY + t * deltaY - y;

        return Math.Sqrt(projectedX * projectedX + projectedY * projectedY);
    }

    private static (double X, double Y) ToMeters(double longitude, double latitude, double referenceLatitude)
    {
        double scale = Math.Cos(referenceLatitude * Math.PI / 180.0);

        return (longitude * MetersPerDegree * scale, latitude * MetersPerDegree);
    }

    public override object? PossibleOutputs()
    {
        return null;
    }

    public bool Equals(DistanceExpression? other)
        => other is not null && (ReferenceEquals(this, other) || _geometry.EqualsTopologically(other._geometry));

    public override bool Equals(object? obj) => obj is DistanceExpression other && Equals(other);

    public override int GetHashCode() => _geometry.GetHashCode();

    public override string ToString()
    {
        var builder = new StringBuilder("distance: ").AppendLine(_geometry.ToString());

        return builder.ToString();
    }
}
