using System.Text.Json.Nodes;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;

namespace VecTiles.Styles.OpenMapTiles.Expressions;

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

    public override object? PossibleOutputs()
    {
        return null;
    }

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

    public override bool Equals(object? obj) => obj is StepExpression other && Equals(other);

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
