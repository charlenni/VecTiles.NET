using VecTiles.Common.Primitives;

namespace VecTiles.Common.Interfaces;

public interface IExpression
{
    object? Evaluate(EvaluationContext ctx);

    object? PossibleOutputs();
}
