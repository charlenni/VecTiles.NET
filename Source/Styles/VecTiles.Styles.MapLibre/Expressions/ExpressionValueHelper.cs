using VecTiles.Common.Primitives;

namespace VecTiles.Styles.MapLibre.Expressions;

/// <summary>
/// Shared helpers for expression evaluation: value checks, conversions and
/// attribute lookup, plus helpers for the structural equality of expressions.
/// </summary>
internal static class ExpressionValueHelper
{
    /// <summary>
    /// Attribute table of the context: feature attributes are preferred.
    /// </summary>
    public static NetTopologySuite.Features.IAttributesTable? GetAttributes(EvaluationContext? ctx)
    {
        return ctx?.Feature?.Attributes ?? ctx?.Attributes;
    }

    /// <summary>
    /// Value of an attribute, or null if the attribute doesn't exist.
    /// GetOptionalValue does the dictionary lookup once; Exists + indexer
    /// would search twice.
    /// </summary>
    public static object? GetAttribute(EvaluationContext? ctx, string key)
    {
        return GetAttributes(ctx)?.GetOptionalValue(key);
    }

    /// <summary>
    /// True if the value is a number. Numeric cases are handled with type
    /// pattern matching like in the old BinaryFilter for maximum performance.
    /// </summary>
    public static bool IsNumber(object? value)
    {
        return value is sbyte or byte or short or ushort or int or uint or long or ulong
            or float or double or decimal;
    }

    /// <summary>
    /// Numeric value of the given value, or null if it isn't a number.
    /// </summary>
    public static double? AsNumber(object? value)
    {
        if (value == null)
            return null;

        if (value is double d)
            return d;

        if (value is float f)
            return f;

        if (value is int i)
            return i;

        if (value is long l)
            return l;

        if (value is decimal m)
            return (double)m;

        if (value is short s)
            return s;

        if (value is byte b)
            return b;

        if (value is sbyte sb)
            return sb;

        if (value is uint ui)
            return ui;

        if (value is ulong ul)
            return ul;

        return null;
    }

    /// <summary>
    /// Truthiness like MapLibre: null and false are falsy, 0 is falsy,
    /// empty strings are falsy.
    /// </summary>
    public static bool IsTruthy(object? value)
    {
        return value switch
        {
            null => false,
            bool b => b,
            double d => d != 0d,
            string s => s.Length > 0,
            _ => IsNumber(value) ? AsNumber(value) != 0d : true
        };
    }

    /// <summary>
    /// String representation with MapLibre semantics: numbers without trailing
    /// decimal places don't get one, booleans are lower case.
    /// </summary>
    public static string ToStringValue(object? value)
    {
        return value switch
        {
            null => string.Empty,
            string s => s,
            bool b => b ? "true" : "false",
            double d => FormatNumber(d),
            float f => FormatNumber(f),
            _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty
        };
    }

    public static string FormatNumber(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            return value.ToString(System.Globalization.CultureInfo.InvariantCulture);

        if (Math.Floor(value) == value && Math.Abs(value) < 1e15)
            return ((long)value).ToString(System.Globalization.CultureInfo.InvariantCulture);

        return value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Compares two values: numbers numerically, strings ordinal, booleans
    /// false < true. Returns null, if the values aren't comparable (e.g.
    /// different types).
    /// </summary>
    public static int? Compare(object? left, object? right)
    {
        if (IsNumber(left) && IsNumber(right))
        {
            var l = AsNumber(left) ?? 0d;
            var r = AsNumber(right) ?? 0d;

            return l < r ? -1 : l > r ? 1 : 0;
        }

        if (left is string leftString && right is string rightString)
            return string.CompareOrdinal(leftString, rightString);

        if (left is bool leftBool && right is bool rightBool)
            return leftBool == rightBool ? 0 : leftBool ? 1 : -1;

        return null;
    }

    /// <summary>
    /// Equality of two values like MapLibre "==": numbers are compared
    /// numerically, every other combination strictly by value and type
    /// (["==", "2", 2] is false).
    /// </summary>
    public static bool AreEqual(object? left, object? right)
    {
        if (left == null || right == null)
            return left == null && right == null;

        // Numeric comparison also for the different numeric types from the
        // attributes vs. the parsed literals
        if (IsNumber(left) && IsNumber(right))
            return AsNumber(left) == AsNumber(right);

        if (left.GetType() != right.GetType())
            return false;

        if (left is object?[] leftArray && right is object?[] rightArray)
            return leftArray.Length == rightArray.Length
                && leftArray.Zip(rightArray, AreEqual).All(equal => equal);

        return Equals(left, right);
    }

    // ==== Helpers for the structural equality of expression trees ====

    public static bool ValuesEqual(object? left, object? right)
    {
        if (ReferenceEquals(left, right))
            return true;

        if (left is object?[] leftArray && right is object?[] rightArray)
            return leftArray.Length == rightArray.Length
                && leftArray.Zip(rightArray, ValuesEqual).All(equal => equal);

        return Equals(left, right);
    }

    public static int ValueHashCode(object? value)
    {
        if (value is object?[] array)
        {
            int hashCode = 0;

            foreach (var item in array)
                hashCode = (hashCode * 31) ^ ValueHashCode(item);

            return hashCode;
        }

        return value?.GetHashCode() ?? 0;
    }
}
