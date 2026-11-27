using System.Text.Json.Nodes;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;

namespace VecTiles.Styles.MapLibre.Expressions;

/// <summary>
/// Implements the "step" expression: ["step", input, base, stop, output, ...]
/// like MapLibre Native. The result value is the output of the greatest stop
/// that is less than or equal to the input, or the base value if the input is
/// less than all stops.
/// </summary>
public class StepExpression : Expression
{
    private readonly IExpression _input;

    private readonly IExpression _base;

    private readonly (double Stop, IExpression Output)[] _stops;

    private StepExpression(IExpression input, IExpression baseValue, (double, IExpression)[] stops)
    {
        _input = input;
        _base = baseValue;
        _stops = stops;
    }

    /// <summary>
    /// Parses a "step" expression from the given JSON array.
    /// </summary>
    /// <param name="array">The JSON array of the expression, starting with the operator name.</param>
    /// <param name="parser">The parser used to parse the arguments and report errors.</param>
    /// <returns>The parsed expression, or null if parsing failed.</returns>
    public static IExpression? Parse(JsonArray array, ExpressionParser parser)
    {
        var length = array.Count;

        // Input + base value + pairs of stops; so an odd number >= 5
        if (length < 5 || length % 2 == 0)
        {
            parser.Error("Expected an input, a base value and pairs of stops and outputs.");
            return null;
        }

        var input = MatchExpression.ParseChild(array[1], 1, parser, typeof(double));
        var baseValue = MatchExpression.ParseChild(array[2], 2, parser);

        if (input == null || baseValue == null)
            return null;

        var stops = new List<(double, IExpression)>();
        double lastStop = double.NegativeInfinity;

        for (int i = 3; i < length - 1; i += 2)
        {
            if (array[i] is not JsonValue stopValue || stopValue.GetValueKind() != System.Text.Json.JsonValueKind.Number)
            {
                parser.Error("Expected a numeric stop.", i);
                return null;
            }

            double stop = stopValue.GetValue<double>();

            if (stop <= lastStop)
            {
                parser.Error("The stops of a 'step' expression must strictly increase.", i);
                return null;
            }

            lastStop = stop;

            var output = MatchExpression.ParseChild(array[i + 1], i + 1, parser);

            if (output == null)
                return null;

            stops.Add((stop, output));
        }

        return new StepExpression(input, baseValue, [.. stops]);
    }

    /// <summary>
    /// Evaluates the input and returns the base value if the input is below
    /// all stops, or the output of the greatest stop less than or equal to
    /// the input.
    /// </summary>
    /// <param name="ctx">The evaluation context.</param>
    /// <returns>The value of the matching stop or the base value, or null if the input cannot be evaluated.</returns>
    public override object? Evaluate(EvaluationContext ctx)
    {
        var inputValue = CompoundOperators.EvaluateNumber(ctx, _input);

        if (inputValue == null)
            return null;

        var result = _base.Evaluate(ctx);

        foreach (var (stop, output) in _stops)
        {
            if (inputValue < stop)
                break;

            result = output.Evaluate(ctx);
        }

        return result;
    }

    /// <summary>
    /// Always returns null: the possible outputs of a "step" expression
    /// cannot be determined statically.
    /// </summary>
    /// <returns>Null, because the possible outputs are unknown.</returns>
    public override object? PossibleOutputs()
    {
        return null;
    }

    /// <summary>
    /// Indicates whether the current object is equal to another
    /// <see cref="StepExpression"/>, comparing input, base value and all stops.
    /// </summary>
    /// <param name="other">The step expression to compare with.</param>
    /// <returns>True if the step expressions are equal; otherwise, false.</returns>
    public bool Equals(StepExpression? other)
    {
        if (other is null)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        if (!_input.Equals(other._input) || !_base.Equals(other._base) || _stops.Length != other._stops.Length)
            return false;

        for (int i = 0; i < _stops.Length; i++)
        {
            if (_stops[i].Stop != other._stops[i].Stop || !_stops[i].Output.Equals(other._stops[i].Output))
                return false;
        }

        return true;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is StepExpression other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            int hashCode = (_input.GetHashCode() * 397) ^ _base.GetHashCode();

            foreach (var (stop, output) in _stops)
                hashCode = (hashCode * 397) ^ stop.GetHashCode() ^ (output?.GetHashCode() ?? 0);

            return hashCode;
        }
    }
}
