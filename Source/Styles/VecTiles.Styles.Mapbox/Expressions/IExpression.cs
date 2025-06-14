using VecTiles.Common.Primitives;

namespace VecTiles.Styles.Mapbox.Expressions;

public interface IExpression
{
    object? Evaluate(EvaluationContext ctx);

    object? PossibleOutputs();
}
