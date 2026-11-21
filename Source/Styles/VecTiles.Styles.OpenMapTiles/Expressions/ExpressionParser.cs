using System.Text;
using System.Text.Json.Nodes;
using VecTiles.Common.Interfaces;
using VecTiles.Styles.OpenMapTiles.Enums;

namespace VecTiles.Styles.OpenMapTiles.Expressions;

/// <summary>
/// Parses MapLibre style expressions from JSON into <see cref="IExpression"/>
/// trees. Tracks parse errors keyed by the path of the token in the expression,
/// and carries the expected result type and the current variable scope.
/// </summary>
public class ExpressionParser 
{
    Dictionary<string, Func<JsonArray, ExpressionParser, IExpression?>> expressionRegistry = new Dictionary<string, Func<JsonArray, ExpressionParser, IExpression?>>
    {
        // Operators with their own Expression classes; operators without an
        // entry fall through to the compound expression registry
        { "at", AtExpression.Parse },
        { "array", AssertionExpression.Parse },
        { "boolean", AssertionExpression.Parse },
        { "case", CaseExpression.Parse },
        { "coalesce", CoalesceExpression.Parse },
        { "collator", CollatorExpression.Parse },
        { "distance", DistanceExpression.Parse },
        { "get", GetExpression.Parse },
        { "in", InExpression.Parse },
        { "interpolate", InterpolateExpression.Parse },
        { "length", LengthExpression.Parse },
        { "let", LetExpression.Parse },
        { "literal", LiteralExpression.Parse },
        { "match", MatchExpression.Parse },
        { "number", AssertionExpression.Parse },
        { "number-format", NumberFormatExpression.Parse },
        { "object", AssertionExpression.Parse },
        { "semiliteral", SemiLiteralExpression.Parse },
        { "step", StepExpression.Parse },
        { "string", AssertionExpression.Parse },
        { "to-boolean", CoercionExpression.Parse },
        { "to-color", CoercionExpression.Parse },
        { "to-number", CoercionExpression.Parse },
        { "to-string", CoercionExpression.Parse },
        { "var", VarExpression.Parse },
        { "within", WithinExpression.Parse },
    };

    /// <summary>Gets the path of the token being parsed, e.g. "[1][2]", used to key errors.</summary>
    public string Key { get; }
    /// <summary>Gets the expected result type of the expression, if any.</summary>
    public Type Expected { get; }
    /// <summary>Gets the parse errors collected so far, keyed by the token path.</summary>
    public Dictionary<string, string> Errors { get; }
    /// <summary>Gets the current variable scope for "var" expression lookup.</summary>
    public Scope Scope { get; }

    /// <summary>
    /// Parses a style expression from a JSON token and returns the resulting
    /// expression tree.
    /// </summary>
    /// <param name="token">The JSON token to parse; the root of the expression.</param>
    /// <param name="expected">The expected result type of the expression, if any.</param>
    /// <returns>The parsed expression, or null if parsing failed.</returns>
    public static IExpression? ParseExpression(JsonNode? token, Type? expected = null)
    {
        var errors = new Dictionary<string, string>();
        var parser = new ExpressionParser("", errors, expected, new Scope(new Dictionary<string, IExpression>()));
        var result = parser.Parse(token, expected);

        if (result == null)
        {
            var combinedErrors = parser.CombinedErrors();

            if (!string.IsNullOrEmpty(combinedErrors))
                System.Diagnostics.Debug.WriteLine("Couldn't parse expression: " + combinedErrors);

            return null;
        }

        return result;
    }

    /// <summary>
    /// Parses a style expression from its JSON string representation.
    /// </summary>
    /// <param name="str">The JSON string of the expression.</param>
    /// <param name="expected">The expected result type of the expression.</param>
    /// <returns>The parsed expression, or null if the string is not an expression array or parsing failed.</returns>
    public static IExpression? ParseExpression(string str, Type expected)
    {
        var json = JsonNode.Parse(str);

        if (json is JsonArray array)
        {
            var parser = new ExpressionParser("", new Dictionary<string, string>(), expected, new Scope(new Dictionary<string, IExpression>()));
            return parser.Parse(array, expected);
        }

        return null;
    }

    /// <summary>
    /// Initializes a new <see cref="ExpressionParser"/> with the given token
    /// path key, shared error dictionary, expected type and variable scope.
    /// </summary>
    /// <param name="key">The path of the token being parsed, used to key errors.</param>
    /// <param name="errors">The dictionary collecting the parse errors.</param>
    /// <param name="expected">The expected result type of the expression, if any.</param>
    /// <param name="scope">The current variable scope for "var" lookup.</param>
    public ExpressionParser(string key, Dictionary<string, string> errors, Type? expected, Scope scope)
    {
        Key = key;
        Errors = errors;
        Expected = expected!;
        Scope = scope;
    }

    /// <summary>
    /// Parses a single expression token: an array is parsed as a named
    /// operator expression, a simple value as a literal. Null tokens or
    /// malformed input record an error and return null.
    /// </summary>
    /// <param name="token">The JSON token to parse.</param>
    /// <param name="expected">The expected result type of the expression, if any.</param>
    /// <returns>The parsed expression, or null if parsing failed.</returns>
    public IExpression? Parse(JsonNode? token, Type? expected)
    {
        IExpression? result = null;

        if (token is JsonArray array)
        {
            var length = array.Count;

            if (length == 0)
            {
                Errors.Add("Expected an array with at least one element. If you wanted a literal array, use [\"literal\", []].", Key);
                return null;
            }

            if (array[0] is not JsonValue operatorValue || !operatorValue.TryGetValue<string>(out var op))
            {
                Errors.Add("Expression name must be a string, but found " + array[0]?.GetValueKind() + " instead. If you wanted a literal array, use [\"literal\", [...]].", Key);
                return null;
            }

            if (expressionRegistry.TryGetValue(op, out var parse))
            {
                result = parse(array, this);
            }
            else
            {
                // Compound operators like "+", "==", "all", "upcase", "rgb"...
                result = CompoundExpression.Parse(array, this);
            }
        }
        else if (token is JsonValue)
        {
            // A simple literal value
            result = new LiteralExpression(MatchExpression.ToValue(token));
        }

        if (result != null)
            return result;

        return null;
    }

    /// <summary>
    /// Parse a child expression. For use by individual Expression.Parse() methods.
    /// </summary>
    /// <param name="token">Node containing the child</param>
    /// <param name="index">Index of child in array</param>
    /// <param name="expected">Expected type for this child</param>
    /// <param name="typeAnnotationOption">Type annotations</param>
    /// <returns>Expression for this child</returns>
    internal IExpression? Parse(JsonNode? token, int index, Type? expected = null, TypeAnnotationOption typeAnnotationOption = TypeAnnotationOption.None) 
    {
        var parser = new ExpressionParser(Key + "[" + index.ToString() + "]", Errors, expected, Scope);
        return parser.Parse(token, expected);
    }

    internal IExpression? Parse(JsonNode? token, int index, Type? expected, Dictionary<string, IExpression> bindings)
    {
        var parser = new ExpressionParser(Key +"[" + index.ToString() + "]", Errors, expected, new Scope(bindings, Scope));
        return parser.Parse(token, expected);
    }

    /// <summary>
    /// Records a parse error at the current token path.
    /// </summary>
    /// <param name="text">The error message.</param>
    public void Error(string text)
    {
        Errors.Add(text, Key);
    }

    /// <summary>
    /// Records a parse error at a child token of the current path.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="child">The index of the child token the error refers to.</param>
    public void Error(string message, long child)
    {
        Errors.Add( message, Key + "[" + child.ToString() + "]");
    }

    /// <summary>
    /// Records a parse error at a grandchild token of the current path.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="child">The index of the child token the error refers to.</param>
    /// <param name="grandchild">The index of the grandchild token the error refers to.</param>
    public void Error(string message, long child, long grandchild)
    {
        Errors.Add( message, Key + "[" + child.ToString() + "][" + grandchild.ToString() + "]");
    }

    /// <summary>
    /// Appends all errors collected by another parser and clears its error list.
    /// </summary>
    /// <param name="parser">The parser whose errors are appended.</param>
    public void AppendErrors(ExpressionParser parser)
    {
        foreach (var entry in parser.Errors)
            Errors.Add(entry.Key, entry.Value);
        parser.ClearErrors();
    }

    /// <summary>Removes all recorded parse errors.</summary>
    public void ClearErrors()
    {
        Errors.Clear();
    }

    /// <summary>
    /// Combines the recorded errors into a single message, each line prefixed
    /// with the token path the error occurred at.
    /// </summary>
    /// <returns>The combined error message, or an empty string if there are no errors.</returns>
    public string CombinedErrors()
    {
        StringBuilder combinedError = new StringBuilder();

        foreach (var error in Errors) 
        {
            combinedError.AppendLine((!string.IsNullOrEmpty(error.Value) ? error.Value + ": " : "") + error.Key);
        }

        return combinedError.ToString();
    }
}
