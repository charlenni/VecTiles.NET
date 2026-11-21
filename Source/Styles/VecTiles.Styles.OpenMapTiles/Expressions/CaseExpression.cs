using System.Text.Json.Nodes;
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

    /// <summary>
    /// Initializes a new instance of the <see cref="CaseExpression"/> class.
    /// </summary>
    /// <param name="branches">Pairs of condition and output expressions, evaluated in order.</param>
    /// <param name="fallback">The output used when no condition is true.</param>
    public CaseExpression((IExpression, IExpression)[] branches, IExpression fallback)
    {
        _branches = branches;
        _fallback = fallback;
    }

    /// <summary>
    /// Parses a "case" expression from the given JSON array.
    /// </summary>
    /// <param name="array">The JSON array of the expression, starting with the operator name.</param>
    /// <param name="parser">The parser used to parse the arguments and report errors.</param>
    /// <returns>The parsed expression, or null if parsing failed.</returns>
    public static IExpression? Parse(JsonArray array, ExpressionParser parser)
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

    /// <summary>
    /// Evaluates the branches in order and returns the output of the first
    /// branch whose condition is truthy, or the fallback output when none is.
    /// </summary>
    /// <param name="ctx">The evaluation context.</param>
    /// <returns>The value of the first matching branch or of the fallback.</returns>
    public override object? Evaluate(EvaluationContext ctx)
    {
        foreach (var (condition, output) in _branches)
        {
            if (IsTruthy(condition.Evaluate(ctx)))
                return output.Evaluate(ctx);
        }

        return _fallback.Evaluate(ctx);
    }

    /// <summary>
    /// Returns the possible outputs of all branch outputs and the fallback.
    /// </summary>
    /// <returns>The possible outputs of the branch and fallback outputs.</returns>
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

    /// <summary>
    /// Indicates whether the current object is equal to another
    /// <see cref="CaseExpression"/>, comparing all branches and the fallback.
    /// </summary>
    /// <param name="other">The case expression to compare with.</param>
    /// <returns>True if the case expressions are equal; otherwise, false.</returns>
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

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is CaseExpression other && Equals(other);

    /// <inheritdoc />
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
