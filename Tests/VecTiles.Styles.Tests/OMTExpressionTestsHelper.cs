using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using VecTiles.Common.Primitives;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Test helper: builds the context for a fixture feature with the given attributes.
/// </summary>
public static class OMTExpressionTestsHelper
{
    /// <summary>
    /// Creates an evaluation context at zoom 14 with a point feature at the origin carrying the given attributes.
    /// </summary>
    /// <param name="attributes">The feature attributes.</param>
    /// <returns>The evaluation context for the created feature.</returns>
    public static EvaluationContext CreateContext(params (string Key, object? Value)[] attributes)
    {
        return CreateContext(new Point(0, 0), attributes);
    }

    /// <summary>
    /// Creates an evaluation context at zoom 14 with a feature of the given geometry carrying the given attributes.
    /// </summary>
    /// <param name="geometry">The feature geometry.</param>
    /// <param name="attributes">The feature attributes.</param>
    /// <returns>The evaluation context for the created feature.</returns>
    public static EvaluationContext CreateContext(Geometry geometry, params (string Key, object? Value)[] attributes)
    {
        var table = new AttributesTable(attributes.ToDictionary(a => a.Key, a => a.Value));

        var feature = new Feature(geometry, table);

        return new EvaluationContext(14).WithFeature(feature);
    }
}
