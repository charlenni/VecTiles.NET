using Newtonsoft.Json.Linq;
using NetTopologySuite.Geometries;

namespace VecTiles.Styles.OpenMapTiles.Expressions;

/// <summary>
/// Parses a GeoJSON geometry (as used by the "distance" and "within"
/// expressions) into the NetTopologySuite representation. The coordinates are
/// in degrees (longitude, latitude), like in the style spec.
/// </summary>
internal static class GeoJsonGeometry
{
    public static Geometry? Parse(JToken token, ExpressionParser parser)
    {
        if (token is not JObject geometry
            || !geometry.TryGetValue("type", out var typeToken)
            || !geometry.TryGetValue("coordinates", out var coordinates))
        {
            parser.Error("Expected a GeoJSON geometry with a type and coordinates.");
            return null;
        }

        try
        {
            return typeToken.ToString() switch
            {
                "Point" => CreatePoint((JArray)coordinates),
                "MultiPoint" => CreatePoints((JArray)coordinates),
                "LineString" => CreateLineString((JArray)coordinates),
                "MultiLineString" => CreateMultiLineString((JArray)coordinates),
                "Polygon" => CreatePolygon((JArray)coordinates),
                "MultiPolygon" => CreateMultiPolygon((JArray)coordinates),
                _ => null
            };
        }
        catch (InvalidCastException)
        {
            parser.Error("Invalid coordinates in GeoJSON geometry.");
            return null;
        }
    }

    private static Coordinate CreateCoordinate(JArray coordinate)
        => new(coordinate[0].Value<double>(), coordinate[1].Value<double>());

    private static Point CreatePoint(JArray coordinates)
        => GeometryFactory.Default.CreatePoint(CreateCoordinate(coordinates));

    private static MultiPoint CreatePoints(JArray coordinates)
        => GeometryFactory.Default.CreateMultiPoint(coordinates.Cast<JArray>().Select(CreatePoint).ToArray());

    private static LineString CreateLineString(JArray coordinates)
        => GeometryFactory.Default.CreateLineString(coordinates.Cast<JArray>().Select(CreateCoordinate).ToArray());

    private static MultiLineString CreateMultiLineString(JArray coordinates)
        => GeometryFactory.Default.CreateMultiLineString(
            coordinates.Cast<JArray>().Select(line => (LineString)CreateLineString(line)).ToArray());

    private static Polygon CreatePolygon(JArray coordinates)
        => GeometryFactory.Default.CreatePolygon(
            GeometryFactory.Default.CreateLinearRing(coordinates.Cast<JArray>().First().Cast<JArray>().Select(CreateCoordinate).ToArray()));

    private static MultiPolygon CreateMultiPolygon(JArray coordinates)
        => GeometryFactory.Default.CreateMultiPolygon(
            coordinates.Cast<JArray>().Select(polygon => CreatePolygon((JArray)polygon)).ToArray());
}
