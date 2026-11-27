using System.Text.Json.Nodes;
using NetTopologySuite.Geometries;

namespace VecTiles.Styles.MapLibre.Expressions;

/// <summary>
/// Parses a GeoJSON geometry (as used by the "distance" and "within"
/// expressions) into the NetTopologySuite representation. The coordinates are
/// in degrees (longitude, latitude), like in the style spec.
/// </summary>
internal static class GeoJsonGeometry
{
    public static Geometry? Parse(JsonNode? token, ExpressionParser parser)
    {
        if (token is not JsonObject geometry
            || !geometry.TryGetPropertyValue("type", out var typeNode)
            || typeNode is not JsonValue typeValue
            || !typeValue.TryGetValue<string>(out var type)
            || !geometry.TryGetPropertyValue("coordinates", out var coordinates)
            || coordinates is not JsonArray coordinateArray)
        {
            parser.Error("Expected a GeoJSON geometry with a type and coordinates.");
            return null;
        }

        try
        {
            return type switch
            {
                "Point" => CreatePoint(coordinateArray),
                "MultiPoint" => CreatePoints(coordinateArray),
                "LineString" => CreateLineString(coordinateArray),
                "MultiLineString" => CreateMultiLineString(coordinateArray),
                "Polygon" => CreatePolygon(coordinateArray),
                "MultiPolygon" => CreateMultiPolygon(coordinateArray),
                _ => null
            };
        }
        catch (Exception e) when (e is InvalidCastException or InvalidOperationException)
        {
            parser.Error("Invalid coordinates in GeoJSON geometry.");
            return null;
        }
    }

    private static Coordinate CreateCoordinate(JsonArray coordinate)
        => new(coordinate[0]!.GetValue<double>(), coordinate[1]!.GetValue<double>());

    private static Point CreatePoint(JsonArray coordinates)
        => GeometryFactory.Default.CreatePoint(CreateCoordinate(coordinates));

    private static MultiPoint CreatePoints(JsonArray coordinates)
        => GeometryFactory.Default.CreateMultiPoint(coordinates.OfType<JsonArray>().Select(CreatePoint).ToArray());

    private static LineString CreateLineString(JsonArray coordinates)
        => GeometryFactory.Default.CreateLineString(coordinates.OfType<JsonArray>().Select(CreateCoordinate).ToArray());

    private static MultiLineString CreateMultiLineString(JsonArray coordinates)
        => GeometryFactory.Default.CreateMultiLineString(
            coordinates.OfType<JsonArray>().Select(line => (LineString)CreateLineString(line)).ToArray());

    private static Polygon CreatePolygon(JsonArray coordinates)
        => GeometryFactory.Default.CreatePolygon(
            GeometryFactory.Default.CreateLinearRing(coordinates.OfType<JsonArray>().First().OfType<JsonArray>().Select(CreateCoordinate).ToArray()));

    private static MultiPolygon CreateMultiPolygon(JsonArray coordinates)
        => GeometryFactory.Default.CreateMultiPolygon(
            coordinates.OfType<JsonArray>().Select(polygon => CreatePolygon(polygon)).ToArray());
}
