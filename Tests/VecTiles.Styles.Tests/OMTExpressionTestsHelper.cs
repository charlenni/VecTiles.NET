using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using VecTiles.Common.Primitives;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Test helper: builds the context for a fixture feature with the given attributes.
/// </summary>
public static class OMTExpressionTestsHelper
{
    public static EvaluationContext CreateContext(params (string Key, object? Value)[] attributes)
    {
        return CreateContext(new Point(0, 0), attributes);
    }

    public static EvaluationContext CreateContext(Geometry geometry, params (string Key, object? Value)[] attributes)
    {
        var table = new AttributesTable(attributes.ToDictionary(a => a.Key, a => a.Value));

        var feature = new Feature(geometry, table);

        return new EvaluationContext(14).WithFeature(feature);
    }
}
