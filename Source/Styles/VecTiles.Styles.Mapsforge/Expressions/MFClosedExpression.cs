using NetTopologySuite.Geometries;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Styles.Mapsforge.Enums;

namespace VecTiles.Styles.Mapsforge.Expressions;

/// <summary>
/// Expression evaluating the closed condition of a Mapsforge render theme rule
/// (attribute "closed"): a way is closed when the style data represents it as
/// an area (polygon); open ways are line geometries.
/// </summary>
public class MFClosedExpression : IExpression
{
    private readonly MapsforgeClosed _closed;

    /// <summary>
    /// Initializes a closed expression.
    /// </summary>
    /// <param name="closed">Closed condition of the rule.</param>
    public MFClosedExpression(MapsforgeClosed closed)
    {
        _closed = closed;
    }

    /// <inheritdoc />
    public object? Evaluate(EvaluationContext ctx)
    {
        switch (_closed)
        {
            case MapsforgeClosed.Any:
                return true;

            case MapsforgeClosed.Yes:
                return ctx.Feature?.Geometry is Polygon or MultiPolygon;

            case MapsforgeClosed.No:
                return ctx.Feature?.Geometry is LineString or MultiLineString;

            default:
                return false;
        }
    }

    /// <inheritdoc />
    public object? PossibleOutputs()
    {
        return new[] { true, false };
    }
}
