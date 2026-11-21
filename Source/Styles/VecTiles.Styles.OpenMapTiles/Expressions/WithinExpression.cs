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

    /// <summary>
    /// Parses a "within" expression from the given JSON array.
    /// </summary>
    /// <param name="array">The JSON array of the expression, starting with the operator name.</param>
    /// <param name="parser">The parser used to parse the arguments and report errors.</param>
    /// <returns>The parsed expression, or null if parsing failed.</returns>
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

    /// <summary>
    /// Evaluates whether the geometry of the evaluated feature intersects the
    /// reference geometry; points on the boundary count as within.
    /// </summary>
    /// <param name="ctx">The evaluation context.</param>
    /// <returns>True if the feature geometry is within the reference geometry; otherwise, false.</returns>
    public override object? Evaluate(EvaluationContext ctx)
    {
        var featureGeometry = ctx.Feature?.Geometry;

        if (featureGeometry == null)
            return false;

        _prepared ??= PreparedGeometryFactory.Prepare(_geometry);

        // Points on the boundary also count as within
        return _prepared.Intersects(featureGeometry);
    }

    /// <summary>
    /// Always returns null: the possible outputs of a "within" expression
    /// cannot be determined statically.
    /// </summary>
    /// <returns>Null, because the possible outputs are unknown.</returns>
    public override object? PossibleOutputs()
    {
        return null;
    }

    /// <summary>
    /// Indicates whether the current object is equal to another
    /// <see cref="WithinExpression"/>, comparing the reference geometries
    /// topologically.
    /// </summary>
    /// <param name="other">The within expression to compare with.</param>
    /// <returns>True if the geometries are equal; otherwise, false.</returns>
    public bool Equals(WithinExpression? other)
        => other is not null && (ReferenceEquals(this, other) || _geometry.EqualsTopologically(other._geometry));

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is WithinExpression other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => _geometry.GetHashCode();
}
