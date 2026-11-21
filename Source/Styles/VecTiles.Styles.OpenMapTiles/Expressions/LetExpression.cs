using System.Text.Json.Nodes;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;

namespace VecTiles.Styles.OpenMapTiles.Expressions;

/// <summary>
/// Implements the "let" expression: ["let", name1, value1, ..., result]. Binds
/// named expressions to variables usable via "var" inside the result, which is
/// evaluated in the extended scope.
/// </summary>
public class LetExpression : Expression
{
    /// <summary>
    /// Parses a "let" expression from the given JSON token.
    /// </summary>
    /// <param name="token">The JSON token of the expression, starting with the operator name.</param>
    /// <param name="parser">The parser used to parse the arguments and report errors.</param>
    /// <returns>The parsed expression, or null if parsing failed.</returns>
    public static IExpression Parse(JsonNode token, ExpressionParser parser) 
    {
        if (token == null || !(token is JsonArray array))
            throw new ArgumentException("");

        var length = array.Count;

        if (length < 4)
        {
            parser.Error("Expected at least 3 arguments, but found " + (length - 1).ToString() + " instead.");
            return null;
        }

        Dictionary<string, IExpression> bindings = new Dictionary<string, IExpression>();

        for(var i = 1; i < length - 1; i += 2) 
        {
            var name = array[i].ToString();
            if (string.IsNullOrEmpty(name)) 
            {
                parser.Error("Expected string, but found " + array[i].GetType().ToString() + " instead.", i);
                return null;
            }

            bool isValidName = name.All(c => (c >= 48 && c <= 57 || c >= 65 && c <= 90 || c >= 97 && c <= 122 || c == '_'));

            if (!isValidName) 
            {
                parser.Error("Variable names must contain only alphanumeric characters or '_'.", 1);
                return null;
            }
    
            var bindingValue = parser.Parse(array[i + 1], null);
    
            if (bindingValue == null) 
            {
                return null;
            }
    
            bindings.Add(name, bindingValue);
        }

        var result = parser.Parse(array[length - 1], length - 1, parser.Expected, bindings);
        
        if (result == null) 
        {
            return null;
        }

        return new LetExpression(bindings, result);
    }

    /// <summary>
    /// Initializes a new <see cref="LetExpression"/> with variable bindings and result.
    /// </summary>
    /// <param name="bindings">The variable names bound to their value expressions.</param>
    /// <param name="result">The expression evaluated in the extended scope.</param>
    public LetExpression(Dictionary<string, IExpression> bindings, IExpression result)
    {
        Bindings = bindings;
        Result = result;
    }

    Dictionary<string, IExpression> Bindings;
    IExpression Result;

    /// <summary>
    /// Evaluates the result expression; the bindings are already resolved
    /// through the scope at parse time.
    /// </summary>
    /// <param name="ctx">The evaluation context.</param>
    /// <returns>The value of the result expression.</returns>
    public override object Evaluate(EvaluationContext ctx)
    {
        return Result.Evaluate(ctx);
    }

    /// <summary>
    /// Returns the possible outputs of the result expression.
    /// </summary>
    /// <returns>The possible output(s) of the result expression.</returns>
    public override object PossibleOutputs()
    {
        return Result.PossibleOutputs(); ;
    }

    /// <summary>
    /// Indicates whether the current object is equal to another
    /// <see cref="LetExpression"/>, comparing all bindings and the result.
    /// </summary>
    /// <param name="other">The let expression to compare with.</param>
    /// <returns>True if the let expressions are equal; otherwise, false.</returns>
    public bool Equals(LetExpression? other)
    {
        if (other is null)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        if (Bindings.Count != other.Bindings.Count || !Result.Equals(other.Result))
            return false;

        foreach (var (name, expression) in Bindings)
        {
            if (!other.Bindings.TryGetValue(name, out var otherExpression) || !expression.Equals(otherExpression))
                return false;
        }

        return true;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is LetExpression other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return Result?.GetHashCode() ?? 0;
    }
}
