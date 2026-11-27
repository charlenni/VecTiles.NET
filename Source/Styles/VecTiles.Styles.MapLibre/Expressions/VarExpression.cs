using System.Text.Json.Nodes;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;

namespace VecTiles.Styles.MapLibre.Expressions;

/// <summary>
/// Implements the "var" expression: ["var", name]. Evaluates to the value
/// expression a variable was bound to by an enclosing "let" expression; the
/// lookup is resolved against the parser scope at parse time.
/// </summary>
public class VarExpression : Expression
{
    /// <summary>
    /// Parses a "var" expression from the given JSON token.
    /// </summary>
    /// <param name="token">The JSON token of the expression, starting with the operator name.</param>
    /// <param name="parser">The parser used to parse the arguments and report errors.</param>
    /// <returns>The parsed expression, or null if parsing failed.</returns>
    public static IExpression Parse(JsonNode token, ExpressionParser parser)
    {
        if (token == null || !(token is JsonArray array))
            throw new ArgumentException("");

        var length = array.Count;

        if (length != 2 || array[1] is not JsonValue nameValue || !nameValue.TryGetValue<string>(out var name))
        {
            parser.Error("'var' expression requires exactly one string literal argument.");
            return null;
        }

        var bindingValue = parser.Scope.GetBinding(name);

        if (bindingValue == null)
        {
            parser.Error("Unknown variable " + name + ". Make sure " + name + " has been bound in an enclosing \"let\" expression before using it.", 1);
            return null;
        }

        return new VarExpression(name, bindingValue);
    }

    /// <summary>
    /// Initializes a new <see cref="VarExpression"/> for the given variable.
    /// </summary>
    /// <param name="name">The name of the variable.</param>
    /// <param name="result">The value expression the variable is bound to.</param>
    public VarExpression(string name, IExpression result)
    {
        Name = name;
        Result = result;
    }

    /// <summary>The name of the variable referenced by this expression.</summary>
    public string Name { get; }

    IExpression Result;

    /// <summary>
    /// Evaluates the bound value expression of the variable.
    /// </summary>
    /// <param name="ctx">The evaluation context.</param>
    /// <returns>The value of the variable's bound expression.</returns>
    public override object? Evaluate(EvaluationContext ctx)
    {
        return Result.Evaluate(ctx);
    }

    /// <summary>
    /// Returns the possible outputs of the variable's bound expression.
    /// </summary>
    /// <returns>The possible output(s) of the bound expression.</returns>
    public override object? PossibleOutputs()
    {
        return Result.PossibleOutputs(); ;
    }

    /// <summary>
    /// Indicates whether the current object is equal to another
    /// <see cref="VarExpression"/>, comparing the variable names.
    /// </summary>
    /// <param name="other">The var expression to compare with.</param>
    /// <returns>True if the variable names are equal; otherwise, false.</returns>
    public bool Equals(VarExpression? other)
    {
        return other is not null && Name == other.Name;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is VarExpression other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => Name.GetHashCode();
}
