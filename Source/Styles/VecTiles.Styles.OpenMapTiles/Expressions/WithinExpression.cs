using System.Text.Json.Nodes;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Prepared;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;

namespace VecTiles.Styles.OpenMapTiles.Expressions;

/// <summary>
/// Implements the "within" expression: ["within", geometry]. True, if the
/// geometry of the evaluated feature is (at least partially) within the given
/// GeoJSON geometry, like MapLibre Native. The check runs on the geometries
/// prepared for repeated tests.
/// </summary>
public class WithinExpression : Expression
{
    private readonly Geometry _geometry;

    private IPreparedGeometry? _prepared;

    private WithinExpression(Geometry geometry)
    {
        _geometry = geometry;
    }

    public static IExpression? Parse(JsonArray array, ExpressionParser parser)
    {
        if (array.Count != 2)
        {
            parser.Error("Expected exactly one geometry argument.");
            return null;
        }

        var geometry = GeoJsonGeometry.Parse(array[1], parser);

        if (geometry == null)
            return null;

        return new WithinExpression(geometry);
    }

    public override object? Evaluate(EvaluationContext ctx)
    {
        var featureGeometry = ctx.Feature?.Geometry;

        if (featureGeometry == null)
            return false;

        _prepared ??= PreparedGeometryFactory.Prepare(_geometry);

        // Points on the boundary also count as within
        return _prepared.Intersects(featureGeometry);
    }

    public override object? PossibleOutputs()
    {
        return null;
    }

    public bool Equals(WithinExpression? other)
        => other is not null && (ReferenceEquals(this, other) || _geometry.EqualsTopologically(other._geometry));

    public override bool Equals(object? obj) => obj is WithinExpression other && Equals(other);

    public override int GetHashCode() => _geometry.GetHashCode();
}
