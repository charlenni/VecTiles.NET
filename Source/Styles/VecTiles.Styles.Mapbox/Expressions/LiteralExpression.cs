using VecTiles.Common.Primitives;
using VecTiles.Styles.Mapbox.Expressions;

public sealed class LiteralExpression : IExpression
{
    private readonly object? _value;

    public LiteralExpression(object? value) => _value = value;

    public object? Evaluate(EvaluationContext ctx) => _value;

    public object? PossibleOutputs() => _value;
}