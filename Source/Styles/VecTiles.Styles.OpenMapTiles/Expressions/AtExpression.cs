using Newtonsoft.Json.Linq;
using VecTiles.Common.Primitives;

namespace VecTiles.Styles.OpenMapTiles.Expressions;

public class AtExpression : Expression
{
    public static IExpression? Parse(JArray array, ExpressionParser parser)
    {
        if (array == null)
            throw new ArgumentException("");

        var length = array.Count;

        if (length != 3)
        {
            parser.Error($"Expected 2 arguments, but found {length - 1} instead.");

            return null;
        }

        var index = parser.Parse(array[1], 1, typeof(double));
        var inputType = parser.Expected != null ? parser.Expected : typeof(object);
        var input = parser.Parse(array[2], 2, inputType);

        if (index == null || input == null) 
            return null;

        return new AtExpression(index, input);
    }

    public AtExpression(IExpression index, IExpression input)
    {
        Index = index;
        Input = input;
    }

    IExpression Index;
    IExpression Input;

    public override object? Evaluate(EvaluationContext ctx)
    {
        var index = CompoundOperators.EvaluateNumber(ctx, Index);

        if (index == null)
            return null;

        return Input.Evaluate(ctx) switch
        {
            object?[] array => (index.Value >= 0 && index.Value < array.Length) ? array[(int)index.Value] : null,
            _ => null
        };
    }

    public override object? PossibleOutputs()
    {
        return null;
    }

    public bool Equals(AtExpression? other)
        => other is not null && (ReferenceEquals(this, other) || (Index.Equals(other.Index) && Input.Equals(other.Input)));

    public override bool Equals(object? obj) => obj is AtExpression other && Equals(other);

    public override int GetHashCode() => (Index.GetHashCode() * 397) ^ Input.GetHashCode();
}
