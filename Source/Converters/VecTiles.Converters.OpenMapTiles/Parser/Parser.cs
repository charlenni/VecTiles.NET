using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using ProtoBuf;
using VecTiles.Common.Enums;
using VecTiles.Common.Primitives;
using VecTiles.Converters.OpenMapTiles.Pbf;

namespace VecTiles.Converters.OpenMapTiles.Parser;

public static class Parser
{
    public static string Id = "id";

    private static GeometryFactory geometryFactory = GeometryFactory.Default;
    private static Geometry fullTile = geometryFactory.ToGeometry(new Envelope(0, 512, 0, 512));

    /// <summary>
    /// Parses an unzipped tile in OpenMapTiles format.
    /// </summary>
    /// <param name="stream">Stream containing tile data in Pbf format.</param>
    /// <param name="inverted">Flag, if the y-axis has to be inverted.</param>
    /// <param name="requestedTile">The tile being requested.</param>
    /// <param name="providedTile">The tile provided in the data stream.</param>
    /// <returns>A VectorTile containing layers and features parsed from the stream.</returns>
    public static VectorTile Parse(Stream stream, Tile requestedTile, Tile providedTile, Scheme scheme)
    {
        // Check if the stream is null or empty
        if (stream == null)
        {
            // If the stream is empty, return an empty VectorTile
            return new VectorTile { TileId = requestedTile.Id };
        }

        // Get tile information from Pbf format
        var pbfTile = Serializer.Deserialize<PbfTile>(stream);

        // Create a new VectorTile to hold the parsed data
        var vectorTile = new VectorTile
        {
            TileId = requestedTile.Id,
        };

        // If the PbfTile is null or has no layers, return an empty VectorTile
        if (pbfTile == null || pbfTile.Layers.Count == 0)
        {
            return vectorTile;
        }

        // Create overzoom transformation if needed
        Overzoom overzoom = Overzoom.CreateFromTiles(requestedTile, providedTile, scheme);

        // Iterate through each layer in the PbfTile
        foreach (var pbfLayer in pbfTile.Layers)
        {
            // Create a new Layer for the VectorTile
            var vectorTileLayer = new Layer
            {
                Name = pbfLayer.Name,
            };

            // Convert all features
            foreach (var pbfFeature in pbfLayer.Features)
            {
                var feature = ParseFeature(pbfFeature, pbfLayer.Name, pbfLayer.Keys, pbfLayer.Values, pbfLayer.Extent, overzoom);

                if (feature != null)
                {
                    // Add the feature to the layer
                    vectorTileLayer.Features.Add(feature);
                }
            }

            // Add the layer to the VectorTile
            vectorTile.Layers.Add(vectorTileLayer);
        }

        return vectorTile;
    }

    private static IFeature? ParseFeature(PbfFeature pbfFeature, string layerName, List<string> keys, List<PbfValue> values, uint extent, Overzoom overzoom)
    {
        var geometry = ParseGeometry(pbfFeature.Geometry, pbfFeature.Type, overzoom);

        if (geometry == null)
        {
            // If geometry is null, skip this feature
            return null;
        }

        var feature = new Feature
        {
            Geometry = geometry,
            Attributes = new AttributesTable()
        };

        // Add tags to attributes
        for (int i = 0; i < pbfFeature.Tags.Count; i += 2)
        {
            var keyIndex = (int)pbfFeature.Tags[i];
            var valueIndex = (int)pbfFeature.Tags[i + 1];
            var value = GetValueForKey(values[valueIndex]);
            feature.Attributes.Add(keys[keyIndex], value);
        }

        // TODO: There are MBTiles files, which contain features with two Ids
        // Set the ID if available
        if (pbfFeature.Id != 0 && !feature.Attributes.Exists(Id))
        {
            feature.Attributes.Add(Id, pbfFeature.Id);
        }

        return feature;
    }

    private static Geometry? ParseGeometry(List<uint> geom, PbfGeomType geomType, Overzoom overzoom)
    {
        var listOfPoints = ParsePoints(geom, geomType, overzoom);

        if (listOfPoints.Count == 0 || listOfPoints[0].Count == 0)
        {
            // If no points are available, return null
            return null;
        }

        Geometry? result = null;

        // Convert list of points to a Geometry object
        switch (geomType)
        {
            case PbfGeomType.Point:
                // For point geometry, create a Point object
                if (listOfPoints.Count == 1)
                {
                    // If there's only one point, create a Point directly
                    result = geometryFactory.CreatePoint(listOfPoints[0][0]);
                }
                else if (listOfPoints.Count > 1)
                {
                    // If there are multiple points, create a MultiPoint
                    result = geometryFactory.CreateMultiPoint(listOfPoints.Select(lop => geometryFactory.CreatePoint(lop[0])).ToArray());
                }
                break;
            case PbfGeomType.LineString:
                if (listOfPoints.Count == 1)
                {
                    // For line geometry, create a LineString object
                    result = geometryFactory.CreateLineString(listOfPoints[0].ToArray());
                }
                else if (listOfPoints.Count > 1)
                {
                    result = geometryFactory.CreateMultiLineString(listOfPoints.Select(lop => geometryFactory.CreateLineString(lop.ToArray())).ToArray());
                }
                break;
            case PbfGeomType.Polygon:
                // Sanity check
                if (listOfPoints.Count == 0)
                {
                    break;
                }
                var polygons = CreatePolygons(listOfPoints);
                result = polygons.Length == 1 ? geometryFactory.CreatePolygon(polygons[0].Shell, polygons[0].Holes) : geometryFactory.CreateMultiPolygon(polygons);
                break;
            default:
                // If the geometry type is unknown, return null
                return null;
        }

        if (result == null || result.IsEmpty)
        {
            // If the resulting geometry is empty, return null
            return null;
        }

        if (!result.Intersects(fullTile))
        {
            // If the geometry does not intersect with the full tile (no
            // part of the geometry is inside of the tile), return null
            return null;
        }

        // If the geometry is valid and intersects with the full tile, return it
        return result;
    }

    /// <summary>
    /// Decodes a list of encoded geometry commands (from OpenMapTiles PBF format) into a list of coordinate lists.
    /// Handles MoveTo, LineTo, and ClosePath commands for Point, LineString, and Polygon geometries.
    /// Optionally applies an overzoom transformation to each coordinate.
    /// </summary>
    /// <param name="geom">The encoded geometry command list.</param>
    /// <param name="geomType">The type of geometry (Point, LineString, Polygon).</param>
    /// <param name="overzoom">The overzoom transformation to apply to coordinates.</param>
    /// <returns>A list of coordinate lists, each representing a geometry part (e.g., ring or line).</returns>
    private static List<List<Coordinate>> ParsePoints(List<uint> geometry, PbfGeomType geomType, Overzoom overzoom)
    {
        const uint CMD_MOVE_TO = 1;
        const uint CMD_LINE_TO = 2;
        const uint CMD_CLOSE_PATH = 7;

        long x = 0, y = 0;
        var listOfPoints = new List<List<Coordinate>>();
        List<Coordinate>? points = null;
        int i = 0;

        while (i < geometry.Count)
        {
            if (i >= geometry.Count) break; // Safety check

            uint commandLength = geometry[i++];
            uint command = commandLength & 0x7;
            uint length = commandLength >> 3;

            switch (command)
            {
                case CMD_MOVE_TO:
                    for (uint j = 0; j < length; j++)
                    {
                        if (i + 1 >= geometry.Count) break; // Safety check
                        long dx = ZigZag.Decode(geometry[i++]);
                        long dy = ZigZag.Decode(geometry[i++]);
                        x += dx;
                        y += dy;
                        points = new List<Coordinate>();
                        listOfPoints.Add(points);
                        points.Add(overzoom.Transform(x, y));
                    }
                    break;

                case CMD_LINE_TO:
                    if (points == null)
                    {
                        points = new List<Coordinate>();
                        listOfPoints.Add(points);
                    }
                    for (uint j = 0; j < length; j++)
                    {
                        if (i + 1 >= geometry.Count) break; // Safety check
                        long dx = ZigZag.Decode(geometry[i++]);
                        long dy = ZigZag.Decode(geometry[i++]);
                        x += dx;
                        y += dy;
                        points.Add(overzoom.Transform(x, y));
                    }
                    break;

                case CMD_CLOSE_PATH:
                    if (geomType == PbfGeomType.Polygon && points != null && points.Count > 0)
                    {
                        // Only close if not already closed
                        if (!points[0].Equals2D(points[^1]))
                            points.Add(points[0]);
                    }
                    break;

                default:
                    // Unknown command, skip
                    break;
            }
        }

        return listOfPoints;
    }

    private static Polygon[] CreatePolygons(List<List<Coordinate>> listOfPoints)
    {
        // Get orientation of first polygon ring
        var orientationCW = IsClockwise(listOfPoints[0]); // ShoelaceArea(listOfPoints[0]) > 0;
        // List of polygons
        var polygons = new List<Polygon>();
        // Check all polygons in the list
        var shell = new LinearRing(orientationCW ? listOfPoints[0].ToArray() : listOfPoints[0].AsEnumerable().Reverse().ToArray());
        // List of holes
        var holes = new List<LinearRing>();
        var i = 1;
        while (i < listOfPoints.Count)
        {
            // Get the current ring
            var points = listOfPoints[i];
            // Check if the ring is valid
            if (points.Count < 3)
            {
                // If the ring has less than 3 points, skip it
                i++;
                continue;
            }
            // Get orientation of this ring
            var orientation = IsClockwise(points); // ShoelaceArea(points) > 0;
            if (orientation == orientationCW)
            {
                // If the orientation matches the first ring, it's a new polygon
                // So save the last one
                polygons.Add(geometryFactory.CreatePolygon(shell, holes.ToArray()));
                // Start a new one
                shell = new LinearRing(orientationCW ? points.ToArray() : points.AsEnumerable().Reverse().ToArray());
                holes.Clear();
            }
            else
            {
                // If the orientation is different, it's a hole
                holes.Add(new LinearRing(orientationCW ? points.ToArray() : points.AsEnumerable().Reverse().ToArray()));
            }
            i++;
        }

        polygons.Add(geometryFactory.CreatePolygon(shell, holes.ToArray()));

        return polygons.ToArray();
    }
    
    /// <summary>
    /// Function to calculate the area of a polygon. If it is CW then area is negative, if CCW then positive
    /// </summary>
    /// <param name="c">List of coordinates for this polygon</param>
    /// <returns>True, if the list of coordinates is clockwise oriented</returns>
    public static bool IsClockwise(List<Coordinate> c)
    {
        if (c == null || c.Count < 3)
            throw new ArgumentException("List of coordinates for polygon must have 3 or more points");

        double sum = 0;
        int n = c.Count;

        for (int i = 0; i < n; i++)
        {
            Coordinate current = c[i];
            Coordinate next = c[(i + 1) % n];
            sum += (next.X - current.X) * (next.Y + current.Y);
        }

        return sum < 0;
    }

    private static object? GetValueForKey(PbfValue value)
    {
        if (value.HasBoolValue) return value.BoolValue;
        if (value.HasDoubleValue) return value.DoubleValue;
        if (value.HasFloatValue) return value.FloatValue;
        if (value.HasIntValue) return value.IntValue;
        if (value.HasSIntValue) return value.SintValue;
        if (value.HasUIntValue) return value.UintValue;
        if (value.HasStringValue) return value.StringValue;
        return null;
    }
}
