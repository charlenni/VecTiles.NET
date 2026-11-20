using System.Globalization;
using Newtonsoft.Json.Linq;
using VecTiles.Common.Extensions;
using VecTiles.Common.Primitives;

namespace VecTiles.Styles.OpenMapTiles.Expressions;

/// <summary>
/// Coercion of expression results into the types used by the stopped value
/// classes. Expressions return raw values (e.g. strings from "get"), so they
/// have to be converted like MapLibre Native coerces property function outputs.
/// </summary>
internal static class StoppedExpression
{
    public static float ToFloat(object? value) => value switch
    {
        null => 0f,
        float f => f,
        string s => float.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var f) ? f : 0f,
        bool b => b ? 1f : 0f,
        IConvertible convertible => Convert.ToSingle(convertible, CultureInfo.InvariantCulture),
        _ => 0f
    };

    public static bool ToBoolean(object? value) => value switch
    {
        null => false,
        bool b => b,
        string s => bool.TryParse(s, out var b) && b,
        IConvertible convertible => Convert.ToBoolean(convertible, CultureInfo.InvariantCulture),
        _ => false
    };

    public static string ToStringValue(object? value) => value switch
    {
        null => string.Empty,
        string s => s,
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
    };

    public static Color ToColor(object? value) => value switch
    {
        null => Colors.Empty,
        Color color => color,
        string s => ColorFromString(s),
        _ => Colors.Empty
    };

    private static Color ColorFromString(string s)
    {
        try
        {
            return s.FromString();
        }
        catch (Exception)
        {
            return Colors.Empty;
        }
    }

    public static float[] ToFloatArray(object? value) => value switch
    {
        null => [],
        float[] array => array,
        IEnumerable<float> array => [.. array],
        JArray array => [.. array.Select(item => ToFloat(item))],
        IEnumerable<object?> array => [.. array.Select(item => ToFloat(item))],
        _ => []
    };

    public static T ToEnum<T>(object? value) where T : struct, Enum
    {
        if (value is string s && Enum.TryParse<T>(s, ignoreCase: true, out var result))
        {
            return result;
        }

        return default;
    }
}
