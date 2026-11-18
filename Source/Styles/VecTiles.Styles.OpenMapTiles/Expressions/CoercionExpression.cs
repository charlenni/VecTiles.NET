using Newtonsoft.Json.Linq;
using VecTiles.Common.Extensions;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;

namespace VecTiles.Styles.OpenMapTiles.Expressions;

/// <summary>
/// Implements the type coercions "to-string", "to-number", "to-boolean" and
/// "to-color" like MapLibre Native: ["to-number", input, fallback...]. If the
/// input can't be coerced, the fallbacks are tried in order; if none works,
/// the evaluation fails (null).
/// </summary>
public class CoercionExpression : Expression
{
    private readonly string _type;

    private readonly IExpression _input;

    private readonly IExpression[] _fallbacks;

    private CoercionExpression(string type, IExpression input, IExpression[] fallbacks)
    {
        _type = type;
        _input = input;
        _fallbacks = fallbacks;
    }

    public static IExpression? Parse(JArray array, ExpressionParser parser)
    {
        if (array.Count < 2)
        {
            parser.Error($"Expected at least one argument, but only found {array.Count - 1}.");
            return null;
        }

        var type = array[0].ToString();

        if (type is not ("to-string" or "to-number" or "to-boolean" or "to-color"))
        {
            parser.Error($"Unknown coercion \"{type}\".");
            return null;
        }

        var input = MatchExpression.ParseChild(array[1], 1, parser);

        if (input == null)
            return null;

        var fallbacks = new List<IExpression>();

        for (int i = 2; i < array.Count; i++)
        {
            var fallback = MatchExpression.ParseChild(array[i], i, parser);

            if (fallback == null)
                return null;

            fallbacks.Add(fallback);
        }

        return new CoercionExpression(type, input, [.. fallbacks]);
    }

    public override object? Evaluate(EvaluationContext ctx)
    {
        var value = _input.Evaluate(ctx);
        var converted = Convert(value);

        if (converted != null)
            return converted;

        foreach (var fallback in _fallbacks)
        {
            var fallbackValue = Convert(fallback.Evaluate(ctx));

            if (fallbackValue != null)
                return fallbackValue;
        }

        return null;
    }

    public override object? PossibleOutputs()
    {
        return null;
    }

    private object? Convert(object? value)
    {
        return _type switch
        {
            "to-string" => value == null ? null : ExpressionValueHelper.ToStringValue(value),
            "to-number" => ExpressionValueHelper.AsNumber(value)
                ?? (value is null ? 0d
                    : value is bool b ? (b ? 1d : 0d)
                    : value is string s && double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var number) ? number
                    : null),
            "to-boolean" => value == null ? false
                : value is bool b ? b
                : ExpressionValueHelper.IsTruthy(value),
            "to-color" => ToColor(value),
            _ => null
        };
    }

    /// <summary>
    /// Converts a value to a MGLColorType. Colors are given as color objects,
    /// as string like the CSS syntax used by
    /// <see cref="VecTiles.Common.Extensions.ColorExtensions"/> or as
    /// numbers (single gray value, or arrays of red/green/blue[/alpha]).
    /// </summary>
    private static object? ToColor(object? value)
    {
        switch (value)
        {
            case Color color:
                return color;

            case string s:
                try
                {
                    return s.FromString();
                }
                catch (Exception)
                {
                    return null;
                }

            case object?[] array when array.Length == 4
                && array.All(item => ExpressionValueHelper.IsNumber(item)):
            {
                var alpha = Math.Clamp((double)ExpressionValueHelper.AsNumber(array[3])!, 0d, 1d);

                return CreateColorFromComponents(array, (byte)Math.Round(alpha * 255.0));
            }

            case object?[] array when array.Length == 3
                && array.All(item => ExpressionValueHelper.IsNumber(item)):
                return CreateColorFromComponents(array, 255);

            default:
                return null;
        }
    }

    private static object? CreateColorFromComponents(object?[] array, byte alpha)
    {
        return new Color(
            (byte)Math.Clamp(Math.Round((double)ExpressionValueHelper.AsNumber(array[0])!), 0, 255),
            (byte)Math.Clamp(Math.Round((double)ExpressionValueHelper.AsNumber(array[1])!), 0, 255),
            (byte)Math.Clamp(Math.Round((double)ExpressionValueHelper.AsNumber(array[2])!), 0, 255),
            alpha);
    }

    public bool Equals(CoercionExpression? other)
    {
        if (other is null)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        if (_type != other._type || !_input.Equals(other._input) || _fallbacks.Length != other._fallbacks.Length)
            return false;

        for (int i = 0; i < _fallbacks.Length; i++)
        {
            if (!_fallbacks[i].Equals(other._fallbacks[i]))
                return false;
        }

        return true;
    }

    public override bool Equals(object? obj) => obj is CoercionExpression other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hashCode = _type.GetHashCode() ^ _input.GetHashCode();

            foreach (var fallback in _fallbacks)
                hashCode = (hashCode * 397) ^ fallback.GetHashCode();

            return hashCode;
        }
    }
}
