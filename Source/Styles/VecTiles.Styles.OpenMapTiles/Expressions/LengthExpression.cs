using Newtonsoft.Json.Linq;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;

namespace VecTiles.Styles.OpenMapTiles.Expressions;

public class LengthExpression : Expression
{
    public static IExpression? Parse(JToken token, ExpressionParser parser)
    {
        if (token == null || token is not JArray array)
            throw new ArgumentException("");

        var length = array.Count;

        if (length != 2)
        {
            parser.Error("Expected one argument, but found " + length.ToString() + " instead.");
            return null;
        }

        var input = parser.Parse(array[1], 1);

        if (input == null)
            return null;

        return new LengthExpression(input);
    }

    public LengthExpression(IExpression input) => Input = input;

    IExpression Input;

    public override object? Evaluate(EvaluationContext ctx)
    {
        return Input.Evaluate(ctx) switch
        {
            string s => (double)s.Length,
            object?[] array => (double)array.Length,
            _ => null
        };
    }

    public override object? PossibleOutputs()
    {
        return null;
    }
}
