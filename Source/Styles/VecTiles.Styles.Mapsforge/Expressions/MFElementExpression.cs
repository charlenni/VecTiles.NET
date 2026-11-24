using NetTopologySuite.Geometries;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Styles.Mapsforge.Enums;

namespace VecTiles.Styles.Mapsforge.Expressions;

/// <summary>
/// Expression evaluating the element type condition of a Mapsforge render theme
/// rule (attribute "e"): nodes are point geometries, ways are linear and area
/// geometries. The element type is decided from the geometry of the feature the
/// style layer is evaluated for.
/// </summary>
public class MFElementExpression : IExpression
{
    private readonly MapsforgeElement _element;

    /// <summary>
    /// Initializes an element type expression.
    /// </summary>
    /// <param name="element">Element type of the rule.</param>
    public MFElementExpression(MapsforgeElement element)
    {
        _element = element;
    }

    /// <inheritdoc />
    public object? Evaluate(EvaluationContext ctx)
    {
        switch (_element)
        {
            case MapsforgeElement.Any:
                return true;

            case MapsforgeElement.Node:
                return ctx.Feature?.Geometry is Point or MultiPoint;

            case MapsforgeElement.Way:
                return ctx.Feature?.Geometry is LineString or MultiLineString or Polygon or MultiPolygon;

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
