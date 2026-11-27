using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using VecTiles.Common.Enums;
using VecTiles.Renderers.Skia.Primitives.Vertices;

namespace VecTiles.Renderers.Skia.Extensions;

/// <summary>
/// Extension methods to convert feature geometry into tessellated vertices
/// for fill and line drawing.
/// </summary>
public static class IFeatureExtensions
{
    /// <summary>
    /// Adds the line geometry of the feature to the vertex builder, expanded
    /// to a stroke of the given maximum width.
    /// </summary>
    /// <param name="feature">Feature whose geometry is converted.</param>
    /// <param name="builder">Vertex builder to add the vertices to.</param>
    /// <param name="maxWidth">Maximum stroke width the geometry is expanded for.</param>
    /// <param name="startCap">Cap style at the start of a line.</param>
    /// <param name="endCap">Cap style at the end of a line.</param>
    /// <param name="join">Join style at line corners.</param>
    /// <param name="miterLimit">Miter limit for miter joins (miter length / half stroke width).</param>
    /// <param name="roundLimit">Maximum miter ratio up to which round joins are converted to miter joins.</param>
    /// <returns>The vertex builder with the added vertices.</returns>
    public static VertexBuilder ToVertices(this IFeature feature,
        VertexBuilder builder,
        float maxWidth,
        StrokeCap startCap,
        StrokeCap endCap,
        StrokeJoin join,
        float miterLimit = 4f,
        float roundLimit = float.PositiveInfinity)
    {
        return LineGeometryToVertices(builder, feature.Geometry, maxWidth, startCap, endCap, join, miterLimit, roundLimit);
    }

    /// <summary>
    /// Adds the filled polygon geometry of the feature to the vertex builder.
    /// </summary>
    /// <param name="feature">Feature whose geometry is converted.</param>
    /// <param name="builder">Vertex builder to add the vertices to.</param>
    /// <param name="join">Join style at polygon corners.</param>
    /// <returns>The vertex builder with the added vertices.</returns>
    public static VertexBuilder ToVertices(this IFeature feature,
        VertexBuilder builder,
        StrokeJoin join)
    {
        return FilledPolygonGeometryToVertices(builder, feature.Geometry, join); 
    }

    /// <summary>
    /// Adds the outline of the polygon geometry of the feature to the vertex
    /// builder, expanded to a stroke of the given maximum width.
    /// </summary>
    /// <param name="feature">Feature whose geometry is converted.</param>
    /// <param name="builder">Vertex builder to add the vertices to.</param>
    /// <param name="maxWidth">Maximum stroke width the outline is expanded for.</param>
    /// <param name="join">Join style at polygon corners.</param>
    /// <param name="miterLimit">Miter limit for miter joins (miter length / half stroke width).</param>
    /// <param name="roundLimit">Maximum miter ratio up to which round joins are converted to miter joins.</param>
    /// <returns>The vertex builder with the added vertices.</returns>
    public static VertexBuilder ToVertices(this IFeature feature,
        VertexBuilder builder,
        float maxWidth,
        StrokeJoin join,
        float miterLimit = 4f,
        float roundLimit = float.PositiveInfinity)
    {
        return OutlinedPolygonGeometryToVertices(builder, feature.Geometry, maxWidth, join, miterLimit, roundLimit);
    }

    private static VertexBuilder LineGeometryToVertices(VertexBuilder builder, Geometry geometry,
            float maxWidth,
            StrokeCap startCap,
            StrokeCap endCap,
            StrokeJoin join,
            float miterLimit,
            float roundLimit)
    {
        switch (geometry)
        {
            case LineString line:
                LineStringToVertices(builder, line, maxWidth, startCap, endCap, join, miterLimit, roundLimit);
                break;

            case MultiLineString multiLine:
                foreach (LineString lineStr in multiLine.Geometries)
                {
                    LineStringToVertices(builder, lineStr, maxWidth, startCap, endCap, join, miterLimit, roundLimit);
                }
                break;

            // Could happen for line layers
            case Polygon polygon:
                OutlinedPolygonGeometryToVertices(builder, polygon, maxWidth, join, miterLimit, roundLimit);
                break;

            // Could happen for line layers
            case MultiPolygon multiPolygon:
                foreach (Polygon polygon in multiPolygon.Geometries)
                {
                    OutlinedPolygonGeometryToVertices(builder, polygon, maxWidth, join, miterLimit, roundLimit);
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
        StrokeJoin join,
        float miterLimit,
        float roundLimit)
    {
        switch (geometry)
        {
            case Polygon polygon:
                OutlinedPolygonToVertices(builder, polygon, maxWidth, join, miterLimit, roundLimit);
                break;

            case MultiPolygon multiPolygon:
                foreach (Polygon poly in multiPolygon.Geometries)
                {
                    OutlinedPolygonToVertices(builder, poly, maxWidth, join, miterLimit, roundLimit);
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
        StrokeJoin join,
        float miterLimit,
        float roundLimit)
    {
        if (line.NumPoints == 0) return null;
        var coords = line.Coordinates;
        builder.AddLines([coords], maxWidth, startCap, endCap, join, miterLimit, roundLimit);
        
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
        StrokeJoin join,
        float miterLimit,
        float roundLimit)
    {
        // Outer ring
        var outer = polygon.ExteriorRing.Coordinates;
        var rings = new List<Coordinate[]> { outer };

        // Inner ring (holes)
        for (int i = 0; i < polygon.NumInteriorRings; i++)
        {
            rings.Add(polygon.GetInteriorRingN(i).Coordinates);
        }

        builder.AddOutline(rings, maxWidth, join, miterLimit, roundLimit);
        
        return builder;
    }
}
