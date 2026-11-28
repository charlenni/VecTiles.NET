using NetTopologySuite.Geometries;

namespace VecTiles.Converters.MLT.Parser;

/// <summary>
/// Builds the NTS geometry of every feature from the decoded streams of an MLT geometry
/// column. The topology is length based: geometries count parts, parts count rings and
/// rings count vertices (see https://maplibre.org/maplibre-tile-spec/specification/v1/,
/// chapter geometry column).
/// </summary>
internal static class GeometryDecoder
{
    // Geometry type values of the types stream
    private const int PointType = 0;
    private const int LineStringType = 1;
    private const int PolygonType = 2;
    private const int MultiPointType = 3;
    private const int MultiLineStringType = 4;
    private const int MultiPolygonType = 5;

    private static readonly GeometryFactory GeometryFactory = GeometryFactory.Default;
    private static readonly Geometry FullTile = GeometryFactory.ToGeometry(new Envelope(0, 512, 0, 512));

    /// <summary>
    /// Decodes the geometry of every feature of a layer.
    /// </summary>
    /// <param name="types">One geometry type value per feature, from the types stream</param>
    /// <param name="geometries">Values of the NumGeometries stream, null if absent</param>
    /// <param name="parts">Values of the NumParts stream, null if absent</param>
    /// <param name="rings">Values of the NumRings stream, null if absent</param>
    /// <param name="vertices">Interleaved x and y coordinates of the vertex buffer,
    /// already resolved against the vertex dictionary</param>
    /// <param name="overzoom">Coordinate transformation to the normalized 512-space</param>
    /// <returns>One geometry per feature, null for features without a usable geometry
    /// or outside the tile</returns>
    internal static Geometry?[] Decode(long[] types, long[]? geometries, long[]? parts, long[]? rings,
        long[] vertices, Overzoom overzoom)
    {
        var ringsPresent = rings != null;
        var geometryIndex = 0;
        var partIndex = 0;
        var ringIndex = 0;
        var vertexIndex = 0;

        var result = new Geometry?[types.Length];
        for (var feature = 0; feature < types.Length; feature++)
        {
            switch (types[feature])
            {
                case PointType:
                {
                    var point = ReadVertex(vertices, ref vertexIndex, overzoom);
                    result[feature] = Finish(GeometryFactory.CreatePoint(point));
                    break;
                }

                case MultiPointType:
                {
                    var count = NextLength(geometries, ref geometryIndex, "NumGeometries");
                    var points = new Point[count];
                    for (var i = 0; i < count; i++)
                        points[i] = GeometryFactory.CreatePoint(ReadVertex(vertices, ref vertexIndex, overzoom));
                    result[feature] = Finish(GeometryFactory.CreateMultiPoint(points));
                    break;
                }

                case LineStringType:
                {
                    // When polygons are also present in the column, the vertex counts of
                    // lines are stored in the NumRings stream instead of NumParts
                    var vertexCount = ringsPresent
                        ? NextLength(rings, ref ringIndex, "NumRings")
                        : NextLength(parts, ref partIndex, "NumParts");
                    result[feature] = Finish(CreateLineString(ReadVertices(vertices, ref vertexIndex, vertexCount, overzoom)));
                    break;
                }

                case MultiLineStringType:
                {
                    var count = NextLength(geometries, ref geometryIndex, "NumGeometries");
                    var lines = new List<LineString>((int)count);
                    for (var i = 0; i < count; i++)
                    {
                        var vertexCount = ringsPresent
                            ? NextLength(rings, ref ringIndex, "NumRings")
                            : NextLength(parts, ref partIndex, "NumParts");
                        var line = CreateLineString(ReadVertices(vertices, ref vertexIndex, vertexCount, overzoom));
                        if (line != null)
                            lines.Add(line);
                    }

                    result[feature] = Finish(GeometryFactory.CreateMultiLineString([.. lines]));
                    break;
                }

                case PolygonType:
                {
                    var ringCount = NextLength(parts, ref partIndex, "NumParts");
                    var polygonRings = ReadRings(rings, ref ringIndex, vertices, ref vertexIndex, ringCount, overzoom);
                    result[feature] = Finish(CreatePolygon(polygonRings));
                    break;
                }

                case MultiPolygonType:
                {
                    var count = NextLength(geometries, ref geometryIndex, "NumGeometries");
                    var polygons = new List<Polygon>((int)count);
                    for (var i = 0; i < count; i++)
                    {
                        var ringCount = NextLength(parts, ref partIndex, "NumParts");
                        var polygonRings = ReadRings(rings, ref ringIndex, vertices, ref vertexIndex, ringCount, overzoom);
                        var polygon = CreatePolygon(polygonRings);
                        if (polygon != null)
                            polygons.Add(polygon);
                    }

                    result[feature] = Finish(GeometryFactory.CreateMultiPolygon([.. polygons]));
                    break;
                }

                default:
                    throw new InvalidDataException($"Unsupported MLT geometry type {types[feature]}");
            }
        }

        return result;
    }

    /// <summary>
    /// Reads the next length of a length stream.
    /// </summary>
    private static long NextLength(long[]? lengths, ref int index, string streamName)
    {
        if (lengths == null)
            throw new InvalidDataException($"MLT geometry column needs a {streamName} stream for this geometry type");
        if (index >= lengths.Length)
            throw new InvalidDataException($"MLT {streamName} stream exhausted");
        return lengths[index++];
    }

    /// <summary>
    /// Reads one vertex and transforms it to the normalized 512-space.
    /// </summary>
    private static Coordinate ReadVertex(long[] vertices, ref int index, Overzoom overzoom)
    {
        if (index + 2 > vertices.Length)
            throw new InvalidDataException("MLT vertex buffer exhausted");
        var coordinate = overzoom.Transform(vertices[index], vertices[index + 1]);
        index += 2;
        return coordinate;
    }

    /// <summary>
    /// Reads the given number of vertices and transforms them to the normalized 512-space.
    /// </summary>
    private static List<Coordinate> ReadVertices(long[] vertices, ref int index, long count, Overzoom overzoom)
    {
        if (index + 2 * count > vertices.Length)
            throw new InvalidDataException("MLT vertex buffer exhausted");
        var coordinates = new List<Coordinate>((int)count);
        for (var i = 0; i < count; i++)
        {
            coordinates.Add(overzoom.Transform(vertices[index], vertices[index + 1]));
            index += 2;
        }

        return coordinates;
    }

    /// <summary>
    /// Reads the rings of one polygon, each closed by repeating its first vertex.
    /// </summary>
    private static List<List<Coordinate>> ReadRings(long[]? rings, ref int ringIndex, long[] vertices,
        ref int vertexIndex, long ringCount, Overzoom overzoom)
    {
        var result = new List<List<Coordinate>>((int)ringCount);
        for (var i = 0; i < ringCount; i++)
        {
            var vertexCount = NextLength(rings, ref ringIndex, "NumRings");
            var ring = ReadVertices(vertices, ref vertexIndex, vertexCount, overzoom);
            if (ring.Count > 0 && !ring[0].Equals2D(ring[^1]))
                ring.Add(ring[0]);
            result.Add(ring);
        }

        return result;
    }

    /// <summary>
    /// Creates a line string, null for less than two coordinates.
    /// </summary>
    private static LineString? CreateLineString(List<Coordinate> coordinates)
    {
        return coordinates.Count < 2 ? null : GeometryFactory.CreateLineString([.. coordinates]);
    }

    /// <summary>
    /// Creates a polygon from its rings: the first ring is the shell, the rest are holes.
    /// The shell is normalized to clockwise orientation, the holes to counter clockwise,
    /// like the MVT parser does. Rings with less than three coordinates are skipped.
    /// </summary>
    private static Polygon? CreatePolygon(List<List<Coordinate>> rings)
    {
        List<Coordinate>? shell = null;
        var shellClockwise = false;
        var holes = new List<LinearRing>();
        foreach (var ring in rings)
        {
            if (ring.Count < 4)
                continue;
            if (shell == null)
            {
                shell = ring;
                shellClockwise = IsClockwise(ring);
                continue;
            }

            // A hole has to be oriented opposite to the clockwise shell, so counter clockwise
            holes.Add(new LinearRing(IsClockwise(ring) ? [.. ring.AsEnumerable().Reverse()] : [.. ring]));
        }

        if (shell == null)
            return null;
        return GeometryFactory.CreatePolygon(
            new LinearRing(shellClockwise ? [.. shell] : [.. shell.AsEnumerable().Reverse()]), [.. holes]);
    }

    /// <summary>
    /// Returns the geometry, or null when it is empty or completely outside of the tile.
    /// </summary>
    private static Geometry? Finish(Geometry? geometry)
    {
        if (geometry == null || geometry.IsEmpty)
            return null;
        return geometry.Intersects(FullTile) ? geometry : null;
    }

    /// <summary>
    /// Calculates the orientation of a closed ring: true for clockwise.
    /// </summary>
    private static bool IsClockwise(List<Coordinate> coordinates)
    {
        double sum = 0;
        for (var i = 0; i < coordinates.Count; i++)
        {
            var current = coordinates[i];
            var next = coordinates[(i + 1) % coordinates.Count];
            sum += (next.X - current.X) * (next.Y + current.Y);
        }

        return sum < 0;
    }
}
