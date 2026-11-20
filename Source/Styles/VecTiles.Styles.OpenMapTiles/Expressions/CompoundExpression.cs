using System.Text.Json.Nodes;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;

namespace VecTiles.Styles.OpenMapTiles.Expressions;

/// <summary>
/// Compound expression like MapLibre Native: a named operator with a fixed
/// signature that applies to the evaluated arguments, e.g. ["+", 1, 2] or
/// ["acos", ["get", "x"]]. The operators are registered in
/// <see cref="CompoundExpressionRegistry"/>.
/// </summary>
public class CompoundExpression : Expression
{
    internal interface IOperator
    {
        string Name { get; }

        /// <summary>
        /// Checks the argument count (and constant literal types, if needed).
        /// Records errors in the parser, if the signature doesn't match.
        /// </summary>
        void CheckArgs(IReadOnlyList<IExpression> args, ExpressionParser parser);

        /// <summary>
        /// Evaluates the operator with the argument expressions. The operator
        /// decides, if and when the arguments are evaluated (e.g. short-circuit
        /// evaluation for "all" and "any" like in the old filters).
        /// </summary>
        object? Evaluate(EvaluationContext ctx, IExpression[] args);
    }

    private readonly IOperator _operator;
    private readonly IExpression[] _args;

    private CompoundExpression(IOperator op, IExpression[] args)
    {
        _operator = op;
        _args = args;
    }

    public static IExpression? Parse(JsonArray array, ExpressionParser parser)
    {
        var op = array[0] is JsonValue operatorValue && operatorValue.TryGetValue<string>(out var operatorName)
            ? CompoundExpressionRegistry.Find(operatorName)
            : null;

        if (op == null)
        {
            parser.Error($"Unknown expression \"{MatchExpression.ToValue(array[0])}\". If you wanted a literal array, use [\"literal\", [...]].");
            return null;
        }

        var args = new IExpression?[array.Count - 1];

        for (int i = 1; i < array.Count; i++)
        {
            args[i - 1] = parser.Parse(array[i], i);

            if (args[i - 1] == null)
                return null;
        }

        op.CheckArgs(args!, parser);

        if (parser.Errors.Count > 0)
            return null;

        // Specializations of the most common filter shapes that the old
        // filter system evaluated directly
        if (args!.Length == 2)
        {
            if (op.Name == "==" || op.Name == "!=")
            {
                var equals = GetEqualsExpression.Create(args[0], args[1], op.Name == "!=");

                if (equals != null)
                    return equals;
            }
            else if (op.Name == "<" || op.Name == "<=")
            {
                var order = GetOrderExpression.Create(args[0], args[1], -1, op.Name == "<=");

                if (order != null)
                    return order;
            }
            else if (op.Name == ">" || op.Name == ">=")
            {
                var order = GetOrderExpression.Create(args[0], args[1], 1, op.Name == ">=");

                if (order != null)
                    return order;
            }
        }

        return new CompoundExpression(op, args!);
    }

    public override object? Evaluate(EvaluationContext ctx)
    {
        return _operator.Evaluate(ctx, _args);
    }

    public override object? PossibleOutputs()
    {
        return null;
    }

    public bool Equals(CompoundExpression? other)
    {
        if (other is null)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        if (_operator.Name != other._operator.Name || _args.Length != other._args.Length)
            return false;

        for (int i = 0; i < _args.Length; i++)
        {
            if (!_args[i].Equals(other._args[i]))
                return false;
        }

        return true;
    }

    public override bool Equals(object? obj) => obj is CompoundExpression other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hashCode = _operator.Name.GetHashCode();

            foreach (var arg in _args)
                hashCode = (hashCode * 397) ^ (arg?.GetHashCode() ?? 0);

            return hashCode;
        }
    }
}
