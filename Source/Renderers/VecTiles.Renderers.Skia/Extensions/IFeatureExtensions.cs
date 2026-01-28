using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using SkiaSharp;

namespace VecTiles.Renderers.Skia.Extensions;

public static class IFeatureExtensions
{
    public static List<SKPath> ToSKPath(this IFeature feature)
    {
        return GeometryToSKPath(feature.Geometry);
    }

    private static List<SKPath> GeometryToSKPath(Geometry geometry)
    {
        SKPath path;
        var paths = new List<SKPath>();

        switch (geometry)
        {
            case Point point:
                path = new SKPath();
                path.AddCircle((float)point.X, (float)point.Y, 3); // kleiner Kreis als Marker
                paths.Add(path);
                break;

            case MultiPoint multiPoint:
                foreach (Point pt in multiPoint.Geometries)
                {
                    path = new SKPath();
                    path.AddCircle((float)pt.X, (float)pt.Y, 3);
                    paths.Add(path);
                }
                break;

            case LineString line:
                path = new SKPath();
                AddLineStringToPath(path, line);
                paths.Add(path);
                break;

            case MultiLineString multiLine:
                foreach (LineString lineStr in multiLine.Geometries)
                {
                    path = new SKPath();
                    AddLineStringToPath(path, lineStr);
                    paths.Add(path);
                }
                break;

            case Polygon polygon:
                path = new SKPath();
                AddPolygonToPath(path, polygon);
                paths.Add(path);
                break;

            case MultiPolygon multiPolygon:
                foreach (Polygon poly in multiPolygon.Geometries)
                {
                    path = new SKPath();
                    AddPolygonToPath(path, poly);
                    paths.Add(path);
                }
                break;

            case GeometryCollection collection:
                foreach (Geometry geom in collection.Geometries)
                {
                    var subPaths = GeometryToSKPath(geom);
                    paths.AddRange(subPaths);
                }
                break;

            default:
                throw new NotSupportedException($"Geometry type '{geometry.GeometryType}' is not supported.");
        }

        return paths;
    }

    private static void AddLineStringToPath(SKPath path, LineString line)
    {
        if (line.NumPoints == 0) return;
        var coords = line.Coordinates;
        path.MoveTo((float)coords[0].X, (float)coords[0].Y);
        for (int i = 1; i < coords.Length; i++)
        {
            path.LineTo((float)coords[i].X, (float)coords[i].Y);
        }
    }

    private static void AddPolygonToPath(SKPath path, Polygon polygon)
    {
        // Outer ring
        AddLinearRingToPath(path, polygon.ExteriorRing, true);

        // Inner ring (holes)
        for (int i = 0; i < polygon.NumInteriorRings; i++)
        {
            AddLinearRingToPath(path, polygon.GetInteriorRingN(i), true);
        }
    }

    private static void AddLinearRingToPath(SKPath path, LineString ring, bool close)
    {
        if (ring.NumPoints == 0) return;
        var coords = ring.Coordinates;
        path.MoveTo((float)coords[0].X, (float)coords[0].Y);
        for (int i = 1; i < coords.Length; i++)
        {
            path.LineTo((float)coords[i].X, (float)coords[i].Y);
        }
        if (close)
        {
            path.Close();
        }
    }
}
