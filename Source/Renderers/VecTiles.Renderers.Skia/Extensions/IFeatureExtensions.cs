using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using VecTiles.Common.Enums;
using VecTiles.Renderers.Skia.Primitives.Vertices;

namespace VecTiles.Renderers.Skia.Extensions;

public static class IFeatureExtensions
{
    public static VertexBuilder ToVertices(this IFeature feature,
        VertexBuilder builder,
        float maxWidth, 
        StrokeCap startCap,
        StrokeCap endCap,
        StrokeJoin join)
    {
        return LineGeometryToVertices(builder, feature.Geometry, maxWidth, startCap, endCap, join); 
    }

    public static VertexBuilder ToVertices(this IFeature feature,
        VertexBuilder builder,
        StrokeJoin join)
    {
        return FilledPolygonGeometryToVertices(builder, feature.Geometry, join); 
    }

    public static VertexBuilder ToVertices(this IFeature feature, 
        VertexBuilder builder,
        float maxWidth, 
        StrokeJoin join)
    {
        return OutlinedPolygonGeometryToVertices(builder, feature.Geometry, maxWidth, join); 
    }
    
    private static VertexBuilder LineGeometryToVertices(VertexBuilder builder, Geometry geometry,
            float maxWidth, 
            StrokeCap startCap,
            StrokeCap endCap,
            StrokeJoin join)
    {
        switch (geometry)
        {
            case LineString line:
                LineStringToVertices(builder, line, maxWidth, startCap, endCap, join);
                break;

            case MultiLineString multiLine:
                foreach (LineString lineStr in multiLine.Geometries)
                {
                    LineStringToVertices(builder, lineStr, maxWidth, startCap, endCap, join);
                }
                break;

            // Could happen for line layers
            case Polygon polygon:
                OutlinedPolygonGeometryToVertices(builder, polygon, maxWidth, join);
                break;

            // Could happen for line layers
            case MultiPolygon multiPolygon:
                foreach (Polygon polygon in multiPolygon.Geometries)
                {
                    OutlinedPolygonGeometryToVertices(builder, polygon, maxWidth, join);
                }
                break;

            case Point point: 
            case MultiPoint multiPoint:
                break;
            
            default:
                throw new NotSupportedException($"Geometry type '{geometry.GeometryType}' is not supported.");
        }

        return builder;
    }

    private static VertexBuilder FilledPolygonGeometryToVertices(VertexBuilder builder, Geometry geometry,
        StrokeJoin join)
    {
        switch (geometry)
        {
            case Polygon polygon:
                FilledPolygonToVertices(builder, polygon);
                break;

            case MultiPolygon multiPolygon:
                foreach (Polygon poly in multiPolygon.Geometries)
                {
                    if (poly is not null)
                    {
                        FilledPolygonToVertices(builder, poly);
                    }
                }
                break;

            case Point point: 
            case MultiPoint multiPoint:
                break;
            
            default:
                throw new NotSupportedException($"Geometry type '{geometry.GeometryType}' is not supported.");
        }

        return builder;
    }

    private static VertexBuilder OutlinedPolygonGeometryToVertices(VertexBuilder builder, Geometry geometry,
        float maxWidth, 
        StrokeJoin join)
    {
        switch (geometry)
        {
            case Polygon polygon:
                OutlinedPolygonToVertices(builder, polygon, maxWidth, join);
                break;

            case MultiPolygon multiPolygon:
                foreach (Polygon poly in multiPolygon.Geometries)
                {
                    OutlinedPolygonToVertices(builder, poly, maxWidth, join);
                }
                break;

            case Point point: 
            case MultiPoint multiPoint:
                break;
            
            default:
                throw new NotSupportedException($"Geometry type '{geometry.GeometryType}' is not supported.");
        }

        return builder;
    }

    private static VertexBuilder LineStringToVertices(VertexBuilder builder, LineString line, 
        float maxWidth, 
        StrokeCap startCap,
        StrokeCap endCap,
        StrokeJoin join)
    {
        if (line.NumPoints == 0) return null;
        var coords = line.Coordinates;
        builder.AddLines([coords], maxWidth, startCap, endCap, join);
        
        return builder;
    }

    private static VertexBuilder FilledPolygonToVertices(VertexBuilder builder, Polygon polygon)
    {
        // Outer ring
        var outer = polygon.ExteriorRing.Coordinates;
        var holes = new List<Coordinate[]>();
        
        // Inner ring (holes)
        for (int i = 0; i < polygon.NumInteriorRings; i++)
        {
            holes.Add(polygon.GetInteriorRingN(i).Coordinates);
        }

        builder.AddFill(outer, holes.ToArray());
        
        return builder;
    }
    
    private static VertexBuilder OutlinedPolygonToVertices(VertexBuilder builder, Polygon polygon,
        float maxWidth, 
        StrokeJoin join)
    {
        // Outer ring
        var outer = polygon.ExteriorRing.Coordinates;
        var rings = new List<Coordinate[]> { outer };
        
        // Inner ring (holes)
        for (int i = 0; i < polygon.NumInteriorRings; i++)
        {
            rings.Add(polygon.GetInteriorRingN(i).Coordinates);
        }
        
        builder.AddOutline(rings, maxWidth, join);
        
        return builder;
    }
}
