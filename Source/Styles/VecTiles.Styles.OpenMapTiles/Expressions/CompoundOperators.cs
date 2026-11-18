using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;

namespace VecTiles.Styles.OpenMapTiles.Expressions;

/// <summary>
/// Registration of all compound expression operators. The semantics follow
/// MapLibre Native. Some operators keep the optimizations of the old filters
/// that the expressions are meant to replace:
/// - "all" / "any": short-circuit evaluation like AllFilter / AnyFilter
/// - "in": HashSet for O(1) lookups with more than 10 values, like InFilter
/// - comparisons: numeric fast paths via type pattern matching, like BinaryFilter
/// </summary>
internal static class CompoundOperators
{
    internal static void Register()
    {
        RegisterArithmetic();
        RegisterLogic();
        RegisterComparisons();
        RegisterMembership();
        RegisterStrings();
        RegisterColors();
        RegisterGlobalProperties();
    }

    // ==== Arithmetic and trigonometry ====

    private static void RegisterArithmetic()
    {
        Register("+", args => args >= 2, EvaluateSum);
        Register("*", args => args >= 2, EvaluateProduct);
        Register("-", args => args is 1 or 2, (ctx, args) =>
            args.Length == 1 ? Negate(EvaluateNumber(ctx, args[0]))
                             : Binary(EvaluateNumber(ctx, args[0]), EvaluateNumber(ctx, args[1]), (a, b) => a - b));
        Register("/", args => args == 2, (ctx, args) =>
            Binary(EvaluateNumber(ctx, args[0]), EvaluateNumber(ctx, args[1]), (a, b) => b == 0 ? null : a / b));
        Register("%", args => args == 2, (ctx, args) =>
            Binary(EvaluateNumber(ctx, args[0]), EvaluateNumber(ctx, args[1]), (a, b) => b == 0 ? null : a % b));
        Register("^", args => args == 2, (ctx, args) =>
            Binary(EvaluateNumber(ctx, args[0]), EvaluateNumber(ctx, args[1]), (a, b) => Math.Pow(a, b)));
        Register("min", args => args >= 1, (ctx, args) => Aggregate(ctx, args, Math.Min));
        Register("max", args => args >= 1, (ctx, args) => Aggregate(ctx, args, Math.Max));
        Register("e", args => args == 0, (ctx, args) => Math.E);

        Register("acos", args => args == 1, (ctx, args) => Unbag(EvaluateNumber(ctx, args[0]), Math.Acos));
        Register("asin", args => args == 1, (ctx, args) => Unbag(EvaluateNumber(ctx, args[0]), Math.Asin));
        Register("atan", args => args == 1, (ctx, args) => Unbag(EvaluateNumber(ctx, args[0]), Math.Atan));
        Register("sqrt", args => args == 1, (ctx, args) => Unbag(EvaluateNumber(ctx, args[0]), Math.Sqrt));
        Register("cos", args => args == 1, (ctx, args) => Unbag(EvaluateNumber(ctx, args[0]), Math.Cos));
        Register("sin", args => args == 1, (ctx, args) => Unbag(EvaluateNumber(ctx, args[0]), Math.Sin));
        Register("tan", args => args == 1, (ctx, args) => Unbag(EvaluateNumber(ctx, args[0]), Math.Tan));
        Register("ln", args => args == 1, (ctx, args) => Unbag(EvaluateNumber(ctx, args[0]), Math.Log));
        Register("log10", args => args == 1, (ctx, args) => Unbag(EvaluateNumber(ctx, args[0]), Math.Log10));
        Register("log2", args => args == 1, (ctx, args) => Unbag(EvaluateNumber(ctx, args[0]), Math.Log2));

        static object? Negate(object? value) => value == null ? null : -(double)value;

        static object? Unbag(object? value, Func<double, double> function)
            => value == null ? null : function((double)value);

        static object? Binary(object? left, object? right, Func<double, double, double?> function)
        {
            if (left == null || right == null)
                return null;

            return function((double)left, (double)right);
        }
    }

    private static object? EvaluateSum(EvaluationContext ctx, IExpression[] args)
    {
        double sum = 0;

        foreach (var arg in args)
        {
            var value = EvaluateNumber(ctx, arg);

            if (value == null)
                return null;

            sum += (double)value;
        }

        return sum;
    }

    private static object? EvaluateProduct(EvaluationContext ctx, IExpression[] args)
    {
        double product = 1;

        foreach (var arg in args)
        {
            var value = EvaluateNumber(ctx, arg);

            if (value == null)
                return null;

            product *= (double)value;
        }

        return product;
    }

    private static object? Aggregate(EvaluationContext ctx, IExpression[] args, Func<double, double, double> function)
    {
        double? result = null;

        foreach (var arg in args)
        {
            var value = EvaluateNumber(ctx, arg);

            if (value == null)
                return null;

            result = result == null ? value : function(result.Value, (double)value);
        }

        return result;
    }

    // ==== Logic: short-circuit evaluation like AllFilter / AnyFilter ====

    private static void RegisterLogic()
    {
        Register("all", args => args >= 1, (ctx, args) =>
        {
            // Short-circuit: stops at the first falsy argument, like AllFilter
            foreach (var arg in args)
            {
                if (!ExpressionValueHelper.IsTruthy(arg.Evaluate(ctx)))
                    return false;
            }

            return true;
        });

        Register("any", args => args >= 1, (ctx, args) =>
        {
            // Short-circuit: stops at the first truthy argument, like AnyFilter
            foreach (var arg in args)
            {
                if (ExpressionValueHelper.IsTruthy(arg.Evaluate(ctx)))
                    return true;
            }

            return false;
        });

        Register("!", args => args == 1, (ctx, args) => !ExpressionValueHelper.IsTruthy(args[0].Evaluate(ctx)));
    }

    // ==== Comparisons ====

    private static void RegisterComparisons()
    {
        Register("==", args => args is 2 or 3, (ctx, args) => EvaluateEquality(ctx, args, expected: true));
        Register("!=", args => args is 2 or 3, (ctx, args) => EvaluateEquality(ctx, args, expected: false));
        Register("<", args => args is 2 or 3, (ctx, args) => EvaluateOrder(ctx, args, -1, allowEqual: false));
        Register("<=", args => args is 2 or 3, (ctx, args) => EvaluateOrder(ctx, args, -1, allowEqual: true));
        Register(">", args => args is 2 or 3, (ctx, args) => EvaluateOrder(ctx, args, 1, allowEqual: false));
        Register(">=", args => args is 2 or 3, (ctx, args) => EvaluateOrder(ctx, args, 1, allowEqual: true));
    }

    private static object? EvaluateEquality(EvaluationContext ctx, IExpression[] args, bool expected)
    {
        var left = args[0].Evaluate(ctx);
        var right = args[1].Evaluate(ctx);

        if (args.Length == 3)
        {
            // Collator comparison for strings
            if (left is not string leftString || right is not string rightString)
                return null;

            if (args[2].Evaluate(ctx) is not Collator collator)
                return null;

            return (collator.Compare(leftString, rightString) == 0) == expected;
        }

        return ExpressionValueHelper.AreEqual(left, right) == expected;
    }

    private static object? EvaluateOrder(EvaluationContext ctx, IExpression[] args, int sign, bool allowEqual)
    {
        var left = args[0].Evaluate(ctx);
        var right = args[1].Evaluate(ctx);

        int comparison;

        if (args.Length == 3)
        {
            // Collator comparison for strings
            if (left is not string leftString || right is not string rightString)
                return null;

            if (args[2].Evaluate(ctx) is not Collator collator)
                return null;

            comparison = collator.Compare(leftString, rightString);
        }
        else
        {
            var compare = ExpressionValueHelper.Compare(left, right);

            // Incomparable types (e.g. number vs. string) are false
            if (compare == null)
                return false;

            comparison = compare.Value;
        }

        return allowEqual ? comparison * sign >= 0 : comparison == sign;
    }

    // ==== Membership: has / in with the HashSet optimization of InFilter ====

    private static void RegisterMembership()
    {
        // ["has", key]: in contrast to the old v7 filter "!has" the key has no
        // negation prefix here
        Register("has", args => args == 1, (ctx, args) =>
        {
            var key = args[0].Evaluate(ctx) as string;

            if (key == null)
                return null;

            return ExpressionValueHelper.GetAttributes(ctx)?.Exists(key) ?? false;
        });

        // The "in" expression keeps the HashSet optimization of InFilter and
        // is implemented separately in InExpression
    }

    // ==== Strings ====

    private static void RegisterStrings()
    {
        Register("concat", args => args >= 1, (ctx, args) =>
        {
            var builder = new System.Text.StringBuilder();

            foreach (var arg in args)
            {
                var value = arg.Evaluate(ctx);

                if (value == null)
                    return null;

                builder.Append(ExpressionValueHelper.ToStringValue(value));
            }

            return builder.ToString();
        });

        Register("upcase", args => args == 1, (ctx, args) =>
            args[0].Evaluate(ctx) switch
            {
                string s => s.ToUpperInvariant(),
                null => null,
                _ => null
            });

        Register("downcase", args => args == 1, (ctx, args) =>
            args[0].Evaluate(ctx) switch
            {
                string s => s.ToLowerInvariant(),
                null => null,
                _ => null
            });

        Register("image", args => args == 1, (ctx, args) =>
            args[0].Evaluate(ctx) is string s ? s : null);

        Register("typeof", args => args == 1, (ctx, args) =>
            TypeOf(args[0].Evaluate(ctx)));

        static string TypeOf(object? value)
        {
            return value switch
            {
                null => "null",
                Color => "color",
                Collator => "collator",
                bool => "boolean",
                _ when ExpressionValueHelper.IsNumber(value) => "number",
                string => "string",
                object?[] => "array",
                _ => "object"
            };
        }
    }

    // ==== Colors ====

    private static void RegisterColors()
    {
        Register("rgb", args => args == 3, (ctx, args) => CreateColor(ctx, args, 255));
        Register("rgba", args => args == 4, (ctx, args) => CreateColor(ctx, args, -1));
    }

    private static object? CreateColor(EvaluationContext ctx, IExpression[] args, int fixedAlpha)
    {
        var colorArgs = new double[args.Length];

        for (int i = 0; i < args.Length; i++)
        {
            var value = EvaluateNumber(ctx, args[i]);

            if (value == null)
                return null;

            colorArgs[i] = (double)value;
        }

        var alpha = fixedAlpha >= 0 ? (byte)fixedAlpha
                                    : (byte)Math.Clamp(Math.Round(colorArgs[3] * 255.0), 0, 255);

        return new Color(
            (byte)Math.Clamp(Math.Round(colorArgs[0]), 0, 255),
            (byte)Math.Clamp(Math.Round(colorArgs[1]), 0, 255),
            (byte)Math.Clamp(Math.Round(colorArgs[2]), 0, 255),
            alpha);
    }

    // ==== Global properties ====

    private static void RegisterGlobalProperties()
    {
        // Zoom and other render pipeline values; heatmap-density, line-progress,
        // accumulated and elevation aren't supplied by the renderer (yet)
        Register("zoom", args => args == 0, (ctx, args) => (double)(ctx.Zoom ?? 0f));
        Register("heatmap-density", args => args == 0, (ctx, args) => 0d);
        Register("line-progress", args => args == 0, (ctx, args) => 0d);
        Register("accumulated", args => args == 0, (ctx, args) => 0d);
        Register("global-state", args => args >= 1, (ctx, args) => null);
        Register("elevation", args => args == 0, (ctx, args) => 0d);
    }

    private static void Register(string name, Func<int, bool> arity, Func<EvaluationContext, IExpression[], object?> evaluate)
    {
        CompoundExpressionRegistry.Add(new Operator(name, arity, evaluate));
    }

    internal static double? EvaluateNumber(EvaluationContext ctx, IExpression arg)
    {
        return ExpressionValueHelper.AsNumber(arg.Evaluate(ctx));
    }

    private class Operator(string name, Func<int, bool> arity, Func<EvaluationContext, IExpression[], object?> evaluate)
        : CompoundExpression.IOperator
    {
        public string Name { get; } = name;

        private readonly Func<int, bool> _arity = arity;

        private readonly Func<EvaluationContext, IExpression[], object?> _evaluate = evaluate;

        public void CheckArgs(IReadOnlyList<IExpression> args, ExpressionParser parser)
        {
            if (!_arity(args.Count))
                parser.Error($"Operator \"{Name}\" expects a different number of arguments, but found {args.Count}.");
        }

        public object? Evaluate(EvaluationContext ctx, IExpression[] args) => _evaluate(ctx, args);
    }
}
