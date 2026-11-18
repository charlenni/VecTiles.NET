using Newtonsoft.Json.Linq;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;

namespace VecTiles.Styles.OpenMapTiles.Expressions;

/// <summary>
/// Implements the "case" expression:
/// ["case", condition1, output1, condition2, output2, ..., fallback]
/// </summary>
public class CaseExpression : Expression
{
    private readonly (IExpression Condition, IExpression Output)[] _branches;
    private readonly IExpression _fallback;

    public CaseExpression((IExpression, IExpression)[] branches, IExpression fallback)
    {
        _branches = branches;
        _fallback = fallback;
    }

    public static IExpression? Parse(JArray array, ExpressionParser parser)
    {
        var length = array.Count;

        // Pairs of condition/output + fallback
        if (length < 4 || length % 2 != 0)
        {
            parser.Error("'case' expression requires pairs of condition and output and a fallback output.");
            return null;
        }

        var branches = new List<(IExpression, IExpression)>();

        for (int i = 1; i < length - 1; i += 2)
        {
            var condition = MatchExpression.ParseChild(array[i], i, parser, typeof(bool));

            if (condition == null)
                return null;

            var output = MatchExpression.ParseChild(array[i + 1], i + 1, parser);

            if (output == null)
                return null;

            branches.Add((condition, output));
        }

        var fallback = MatchExpression.ParseChild(array[length - 1], length - 1, parser);

        if (fallback == null)
            return null;

        return new CaseExpression([.. branches], fallback);
    }

    public override object? Evaluate(EvaluationContext ctx)
    {
        foreach (var (condition, output) in _branches)
        {
            if (IsTruthy(condition.Evaluate(ctx)))
                return output.Evaluate(ctx);
        }

        return _fallback.Evaluate(ctx);
    }

    public override object? PossibleOutputs()
    {
        return _branches.Select(b => b.Output).Append(_fallback).Select(o => o.PossibleOutputs());
    }

    private static bool IsTruthy(object? value)
    {
        return value switch
        {
            null => false,
            bool b => b,
            sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal => Convert.ToDouble(value) != 0d,
            string s => s.Length > 0,
            _ => true
        };
    }

    public bool Equals(CaseExpression? other)
    {
        if (other is null)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        if (_branches.Length != other._branches.Length || !_fallback.Equals(other._fallback))
            return false;

        for (int i = 0; i < _branches.Length; i++)
        {
            if (!_branches[i].Condition.Equals(other._branches[i].Condition)
                || !_branches[i].Output.Equals(other._branches[i].Output))
                return false;
        }

        return true;
    }

    public override bool Equals(object? obj) => obj is CaseExpression other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hashCode = _fallback?.GetHashCode() ?? 0;

            foreach (var (_, output) in _branches)
                hashCode = (hashCode * 397) ^ (output?.GetHashCode() ?? 0);

            return hashCode;
        }
    }
}
