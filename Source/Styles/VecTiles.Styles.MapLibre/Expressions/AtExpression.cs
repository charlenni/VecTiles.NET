using System.Text.Json.Nodes;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;

namespace VecTiles.Styles.MapLibre.Expressions;

/// <summary>
/// Implements the "at" expression: ["at", index, input]. Evaluates the
/// array-like input and returns its element at the given zero-based index,
/// or null when the index is out of range or the input is not an array.
/// </summary>
public class AtExpression : Expression
{
    /// <summary>
    /// Parses an "at" expression from the given JSON array.
    /// </summary>
    /// <param name="array">The JSON array of the expression, starting with the operator name.</param>
    /// <param name="parser">The parser used to parse the arguments and report errors.</param>
    /// <returns>The parsed expression, or null if parsing failed.</returns>
    public static IExpression? Parse(JsonArray array, ExpressionParser parser)
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

    /// <summary>
    /// Initializes a new instance of the <see cref="AtExpression"/> class.
    /// </summary>
    /// <param name="index">The expression yielding the zero-based index.</param>
    /// <param name="input">The expression yielding the array to index into.</param>
    public AtExpression(IExpression index, IExpression input)
    {
        Index = index;
        Input = input;
    }

    IExpression Index;
    IExpression Input;

    /// <summary>
    /// Evaluates the input and returns the element at the evaluated index,
    /// or null when the index is out of range or the input is not an array.
    /// </summary>
    /// <param name="ctx">The evaluation context.</param>
    /// <returns>The array element at the given index, or null.</returns>
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

    /// <summary>
    /// Always returns null: the possible outputs of an "at" expression cannot
    /// be determined statically.
    /// </summary>
    /// <returns>Null, because the possible outputs are unknown.</returns>
    public override object? PossibleOutputs()
    {
        return null;
    }

    /// <summary>
    /// Indicates whether the current object is equal to another
    /// <see cref="AtExpression"/>, comparing index and input expression.
    /// </summary>
    /// <param name="other">The expression to compare with.</param>
    /// <returns>True if the expressions are equal; otherwise, false.</returns>
    public bool Equals(AtExpression? other)
        => other is not null && (ReferenceEquals(this, other) || (Index.Equals(other.Index) && Input.Equals(other.Input)));

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is AtExpression other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => (Index.GetHashCode() * 397) ^ Input.GetHashCode();
}
