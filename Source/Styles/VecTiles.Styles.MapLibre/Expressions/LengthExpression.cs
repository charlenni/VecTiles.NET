using System.Text.Json.Nodes;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;

namespace VecTiles.Styles.MapLibre.Expressions;

/// <summary>
/// Implements the "length" expression: ["length", input]. Returns the length
/// of the string or the number of elements of the array input.
/// </summary>
public class LengthExpression : Expression
{
    /// <summary>
    /// Parses a "length" expression from the given JSON token.
    /// </summary>
    /// <param name="token">The JSON token of the expression, starting with the operator name.</param>
    /// <param name="parser">The parser used to parse the arguments and report errors.</param>
    /// <returns>The parsed expression, or null if parsing failed.</returns>
    public static IExpression? Parse(JsonNode token, ExpressionParser parser)
    {
        if (token == null || token is not JsonArray array)
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

    /// <summary>
    /// Initializes a new <see cref="LengthExpression"/> with the given input expression.
    /// </summary>
    /// <param name="input">The expression yielding the string or array to measure.</param>
    public LengthExpression(IExpression input) => Input = input;

    IExpression Input;

    /// <summary>
    /// Evaluates the input and returns its length: the number of characters
    /// for strings and the number of elements for arrays; null for other types.
    /// </summary>
    /// <param name="ctx">The evaluation context.</param>
    /// <returns>The length of the input, or null if the input is neither string nor array.</returns>
    public override object? Evaluate(EvaluationContext ctx)
    {
        return Input.Evaluate(ctx) switch
        {
            string s => (double)s.Length,
            object?[] array => (double)array.Length,
            _ => null
        };
    }

    /// <summary>
    /// Always returns null: the possible outputs of a "length" expression
    /// cannot be determined statically.
    /// </summary>
    /// <returns>Null, because the possible outputs are unknown.</returns>
    public override object? PossibleOutputs()
    {
        return null;
    }
}
