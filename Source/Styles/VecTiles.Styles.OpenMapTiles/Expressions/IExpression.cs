using VecTiles.Common.Primitives;

namespace VecTiles.Styles.OpenMapTiles.Expressions;

public interface IExpression
{
    object? Evaluate(EvaluationContext ctx);

    object? PossibleOutputs();
}
