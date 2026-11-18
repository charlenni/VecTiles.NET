using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VecTiles.Common.Interfaces;
using VecTiles.Styles.OpenMapTiles.Enums;

namespace VecTiles.Styles.OpenMapTiles.Expressions;

public class ExpressionParser 
{
    Dictionary<string, Func<JArray, ExpressionParser, IExpression>> expressionRegistry = new Dictionary<string, Func<JArray, ExpressionParser, IExpression>>
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

    public string Key { get; }
    public Type Expected { get; }
    public Dictionary<string, string> Errors { get; }
    public Scope Scope { get; }

    public static IExpression? ParseExpression(JToken token, Type? expected = null)
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

    public static IExpression ParseExpression(string str, Type expected)
    {
        var json = JsonConvert.DeserializeObject(str);

        if (json is JArray array)
        {
            var parser = new ExpressionParser("", new Dictionary<string, string>(), expected, new Scope(new Dictionary<string, IExpression>()));
            return parser.Parse(array, expected);
        }

        return null;
    }

    public ExpressionParser(string key, Dictionary<string, string> errors, Type expected, Scope scope)
    {
        Key = key;
        Errors = errors;
        Expected = expected;
        Scope = scope;
    }

    public IExpression Parse(JToken token, Type expected)
    {
        IExpression result = null;

        if (token is JArray array)
        {
            var length = array.Count;

            if (length == 0)
            {
                Errors.Add("Expected an array with at least one element. If you wanted a literal array, use [\"literal\", []].", Key);
                return null;
            }

            if (array[0].Type != JTokenType.String)
            {
                Errors.Add("Expression name must be a string, but found " + array[0].GetType() + " instead. If you wanted a literal array, use [\"literal\", [...]].", Key);
                return null;
            }

            var op = array[0].ToString();

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
        else if (token is JValue)
        {
            // A simple literal value
            result = new LiteralExpression(MatchExpression.ToValue(token));
        }

        if (result != null)
            return result;

        return null;
    }

    //internal IExpression ParseExpression(JToken token, TypeAnnotationOption typeAnnotationOption = TypeAnnotationOption.None)
    //{
    //    return Parse(token, typeAnnotationOption);
    //}

    /// <summary>
    /// Parse a child expression. For use by individual Expression.Parse() methods.
    /// </summary>
    /// <param name="array">Array containing the child</param>
    /// <param name="index">Index of child in array</param>
    /// <param name="expected">Expected type for this child</param>
    /// <param name="typeAnnotationOption">Type annotations</param>
    /// <returns>Expression for this child</returns>
    internal IExpression Parse(JToken token, int index, Type expected = null, TypeAnnotationOption typeAnnotationOption = TypeAnnotationOption.None) 
    {
        var parser = new ExpressionParser(Key + "[" + index.ToString() + "]", Errors, expected, Scope);
        return parser.Parse(token, expected);
    }

    internal IExpression Parse(JToken token, int index, Type expected, Dictionary<string, IExpression> bindings)
    {
        var parser = new ExpressionParser(Key +"[" + index.ToString() + "]", Errors, expected, new Scope(bindings, Scope));
        return parser.Parse(token, expected);
    }

    public void Error(string text)
    {
        Errors.Add(text, Key);
    }

    public void Error(string message, long child)
    {
        Errors.Add( message, Key + "[" + child.ToString() + "]");
    }

    public void Error(string message, long child, long grandchild)
    {
        Errors.Add( message, Key + "[" + child.ToString() + "][" + grandchild.ToString() + "]");
    }

    public void AppendErrors(ExpressionParser parser)
    {
        foreach (var entry in parser.Errors)
            Errors.Add(entry.Key, entry.Value);
        parser.ClearErrors();
    }

    public void ClearErrors()
    {
        Errors.Clear();
    }

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
