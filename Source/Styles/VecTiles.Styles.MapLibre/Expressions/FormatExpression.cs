using System.Text;
using System.Text.Json.Nodes;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;

namespace VecTiles.Styles.MapLibre.Expressions;

/// <summary>
/// Implements the "format" expression: ["format", input, options, input, options, ...].
/// Builds a text from the string inputs of its sections. The per section options
/// ("font-scale", "text-font", "text-max-width") are parsed, but not evaluated: the
/// renderer styles the whole text uniformly with the layout font of the symbol, there
/// are no per section text properties.
/// </summary>
public class FormatExpression : Expression
{
    private readonly IExpression[] _inputs;

    private FormatExpression(IExpression[] inputs)
    {
        _inputs = inputs;
    }

    /// <summary>
    /// Parses a "format" expression from the given JSON array: alternating string inputs
    /// and options objects. The options of a section are optional, an input without
    /// options uses the default text properties.
    /// </summary>
    /// <param name="array">The JSON array of the expression, starting with the operator name.</param>
    /// <param name="parser">The parser used to parse the inputs and report errors.</param>
    /// <returns>The parsed expression, or null if parsing failed.</returns>
    public static IExpression? Parse(JsonArray array, ExpressionParser parser)
    {
        if (array.Count < 2)
        {
            parser.Error("Expected at least one input, but only found 0.");
            return null;
        }

        var inputs = new List<IExpression>();

        for (var i = 1; i < array.Count; i += 2)
        {
            var input = parser.Parse(array[i], i, typeof(string));

            if (input == null)
                return null;

            inputs.Add(input);

            if (i + 1 < array.Count)
            {
                // The options object of the section. Its values may be expressions
                // (e.g. "text-font": ["case", ...]); they are not evaluated, so they
                // are only checked for known keys.
                if (array[i + 1] is not JsonObject options)
                {
                    parser.Error("Format section options must be an object.", i + 1);
                    return null;
                }

                foreach (var option in options)
                {
                    if (option.Key is not ("font-scale" or "text-font" or "text-max-width"))
                    {
                        parser.Error("Unknown format option \"" + option.Key + "\".", i + 1);
                        return null;
                    }
                }
            }
        }

        return new FormatExpression([.. inputs]);
    }

    /// <summary>
    /// Evaluates the section inputs and concatenates them into the text. A null input
    /// (e.g. a "coalesce" over missing name attributes) contributes nothing, so the
    /// remaining sections still form the text.
    /// </summary>
    /// <param name="ctx">The evaluation context.</param>
    /// <returns>The concatenated text, or an empty string when all inputs are null.</returns>
    public override object? Evaluate(EvaluationContext ctx)
    {
        var builder = new StringBuilder();

        foreach (var input in _inputs)
        {
            var value = input.Evaluate(ctx);

            if (value != null)
                builder.Append(StoppedExpression.ToStringValue(value));
        }

        return builder.ToString();
    }

    /// <summary>
    /// Always returns null: the possible outputs of a "format" expression
    /// cannot be determined statically.
    /// </summary>
    /// <returns>Null, because the possible outputs are unknown.</returns>
    public override object? PossibleOutputs()
    {
        return null;
    }

    /// <summary>
    /// Indicates whether the current object is equal to another
    /// <see cref="FormatExpression"/>, comparing all input expressions.
    /// </summary>
    /// <param name="other">The format expression to compare with.</param>
    /// <returns>True if the format expressions are equal; otherwise, false.</returns>
    public bool Equals(FormatExpression? other)
    {
        if (other is null)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        if (_inputs.Length != other._inputs.Length)
            return false;

        for (int i = 0; i < _inputs.Length; i++)
        {
            if (!_inputs[i].Equals(other._inputs[i]))
                return false;
        }

        return true;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is FormatExpression other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        int hashCode = 0;

        foreach (var input in _inputs)
            hashCode = (hashCode * 397) ^ input.GetHashCode();

        return hashCode;
    }
}
