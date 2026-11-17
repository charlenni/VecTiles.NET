using Newtonsoft.Json.Linq;
using VecTiles.Common.Primitives;

namespace VecTiles.Styles.OpenMapTiles.Expressions;

/// <summary>
/// Implements the "interpolate" expression: ["interpolate", interpolationType,
/// input, stop, output, ...] like MapLibre Native. Supported interpolation
/// types are "linear" and "exponential" with a base; the outputs must be
/// numbers.
/// </summary>
public class InterpolateExpression : Expression
{
    private readonly double _exponentialBase;

    private readonly IExpression _input;

    private readonly (double Stop, IExpression Output)[] _stops;

    private InterpolateExpression(double exponentialBase, IExpression input, (double, IExpression)[] stops)
    {
        _exponentialBase = exponentialBase;
        _input = input;
        _stops = stops;
    }

    public static IExpression? Parse(JArray array, ExpressionParser parser)
    {
        var length = array.Count;

        // Interpolation + input + pairs of stops; so an odd number >= 5
        if (length < 5 || length % 2 == 0)
        {
            parser.Error("Expected an interpolation type, an input and pairs of stops and outputs.");
            return null;
        }

        double exponentialBase;

        if (array[1] is JArray interpolation && interpolation.Count > 0)
        {
            switch (interpolation[0].ToString())
            {
                case "linear" when interpolation.Count == 1:
                    exponentialBase = 1;
                    break;

                case "exponential" when interpolation.Count == 2
                    && interpolation[1].Type is (JTokenType.Integer or JTokenType.Float):
                    exponentialBase = (double)interpolation[1];
                    break;

                case "cubic-bezier":
                    parser.Error("Cubic bezier interpolation isn't supported yet.");
                    return null;

                default:
                    parser.Error("Invalid interpolation type.");
                    return null;
            }
        }
        else
        {
            parser.Error("Invalid interpolation type.");
            return null;
        }

        var input = MatchExpression.ParseChild(array[2], 2, parser);
        if (input == null)
            return null;

        var stops = new List<(double, IExpression)>();
        double lastStop = double.NegativeInfinity;

        for (int i = 3; i < length - 1; i += 2)
        {
            if (array[i].Type is not (JTokenType.Integer or JTokenType.Float))
            {
                parser.Error("Expected a numeric stop.", i);
                return null;
            }

            double stop = (double)array[i];

            if (stop <= lastStop)
            {
                parser.Error("The stops of an 'interpolate' expression must strictly increase.", i);
                return null;
            }

            lastStop = stop;

            var output = MatchExpression.ParseChild(array[i + 1], i + 1, parser);
            if (output == null)
                return null;

            stops.Add((stop, output));
        }

        return new InterpolateExpression(exponentialBase, input, [.. stops]);
    }

    public override object? Evaluate(EvaluationContext ctx)
    {
        var inputValue = CompoundOperators.EvaluateNumber(ctx, _input);

        if (inputValue == null)
            return null;

        // Below the first stop or above the last stop: nearest output
        if (inputValue <= _stops[0].Stop)
            return _stops[0].Output.Evaluate(ctx);

        for (int i = 1; i < _stops.Length; i++)
        {
            if (inputValue <= _stops[i].Stop)
            {
                var lower = _stops[i - 1].Output.Evaluate(ctx);
                var upper = _stops[i].Output.Evaluate(ctx);
                var fraction = InterpolationFraction(inputValue.Value, _stops[i - 1].Stop, _stops[i].Stop);

                return Interpolate(lower, upper, fraction);
            }
        }

        return _stops[^1].Output.Evaluate(ctx);
    }

    /// <summary>
    /// Interpolates two outputs. Numbers (and colors, which are interpolated
    /// channel by channel like MapLibre) are blended, everything else takes
    /// the lower output.
    /// </summary>
    private static object? Interpolate(object? lower, object? upper, double fraction)
    {
        var lowerNumber = ExpressionValueHelper.AsNumber(lower);
        var upperNumber = ExpressionValueHelper.AsNumber(upper);

        if (lowerNumber != null && upperNumber != null)
            return lowerNumber.Value + (upperNumber.Value - lowerNumber.Value) * fraction;

        if (lower is Color lowerColor && upper is Color upperColor)
        {
            byte Lerp(byte l, byte u) => (byte)Math.Round(l + (u - l) * fraction);

            return new Color(
                Lerp(lowerColor.R, upperColor.R),
                Lerp(lowerColor.G, upperColor.G),
                Lerp(lowerColor.B, upperColor.B),
                Lerp(lowerColor.A, upperColor.A));
        }

        return lower;
    }

    private double InterpolationFraction(double input, double lowerStop, double upperStop)
    {
        if (_exponentialBase == 1d)
            return (input - lowerStop) / (upperStop - lowerStop);

        var lower = Math.Pow(_exponentialBase, input - lowerStop) - 1;
        var upper = Math.Pow(_exponentialBase, upperStop - lowerStop) - 1;

        return upper == 0 ? 0 : lower / upper;
    }

    public override object? PossibleOutputs()
    {
        return null;
    }

    public bool Equals(InterpolateExpression? other)
    {
        if (other is null)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        if (_exponentialBase != other._exponentialBase || !_input.Equals(other._input) || _stops.Length != other._stops.Length)
            return false;

        for (int i = 0; i < _stops.Length; i++)
        {
            if (_stops[i].Stop != other._stops[i].Stop || !_stops[i].Output.Equals(other._stops[i].Output))
                return false;
        }

        return true;
    }

    public override bool Equals(object? obj) => obj is InterpolateExpression other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hashCode = _exponentialBase.GetHashCode() ^ _input.GetHashCode();

            foreach (var (stop, _) in _stops)
                hashCode = (hashCode * 397) ^ stop.GetHashCode();

            return hashCode;
        }
    }
}
