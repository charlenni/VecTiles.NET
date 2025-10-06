using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using SkiaSharp;

namespace VecTiles.Renderers.Skia.Extensions;

public static class IFeatureExtensions
{
    public static SKPath ToSKPath(this IFeature feature)
    {
        return GeometryToSKPath(feature.Geometry);
    }

    private static SKPath GeometryToSKPath(Geometry geometry)
    { 
        var path = new SKPath();

        switch (geometry)
        {
            case Point point:
                path.AddCircle((float)point.X, (float)point.Y, 3); // kleiner Kreis als Marker
                break;

            case MultiPoint multiPoint:
                foreach (Point pt in multiPoint.Geometries)
                {
                    path.AddCircle((float)pt.X, (float)pt.Y, 3);
                }
                break;

            case LineString line:
                AddLineStringToPath(path, line);
                break;

            case MultiLineString multiLine:
                foreach (LineString lineStr in multiLine.Geometries)
                {
                    AddLineStringToPath(path, lineStr);
                }
                break;

            case Polygon polygon:
                AddPolygonToPath(path, polygon);
                break;

            case MultiPolygon multiPolygon:
                foreach (Polygon poly in multiPolygon.Geometries)
                {
                    AddPolygonToPath(path, poly);
                }
                break;

            case GeometryCollection collection:
                foreach (Geometry geom in collection.Geometries)
                {
                    var subPath = GeometryToSKPath(geom);
                    path.AddPath(subPath);
                }
                break;

            default:
                throw new NotSupportedException($"Geometry type '{geometry.GeometryType}' is not supported.");
        }

        return path;
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
