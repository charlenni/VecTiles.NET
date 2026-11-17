using Newtonsoft.Json.Linq;
using VecTiles.Common.Primitives;
using VecTiles.Styles.OpenMapTiles.Expressions;

public sealed class LiteralExpression : IExpression
{
    private readonly object? _value;

    public LiteralExpression(object? value) => _value = value;

    public static IExpression? Parse(JArray array, ExpressionParser parser)
    {
        if (array.Count != 2)
        {
            parser.Error("'literal' expression requires exactly one argument.");
            return null;
        }

        return new LiteralExpression(MatchExpression.ToValue(array[1]));
    }

    public object? Evaluate(EvaluationContext ctx) => _value;

    public object? PossibleOutputs() => _value;

    public bool Equals(LiteralExpression? other)
    {
        return other is not null && (ReferenceEquals(this, other) || ExpressionValueHelper.ValuesEqual(_value, other._value));
    }

    public override bool Equals(object? obj) => obj is LiteralExpression other && Equals(other);

    public override int GetHashCode() => ExpressionValueHelper.ValueHashCode(_value);
}
