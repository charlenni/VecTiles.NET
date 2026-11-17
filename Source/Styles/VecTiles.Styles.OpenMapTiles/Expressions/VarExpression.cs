using Newtonsoft.Json.Linq;
using VecTiles.Common.Primitives;

namespace VecTiles.Styles.OpenMapTiles.Expressions;

public class VarExpression : Expression
{
    public static IExpression Parse(JToken token, ExpressionParser parser)
    {
        if (token == null || !(token is JArray array))
            throw new ArgumentException("");

        var length = array.Count;

        if (length != 2 || !(array[1].Type == JTokenType.String))
        {
            parser.Error("'var' expression requires exactly one string literal argument.");
            return null;
        }

        string name = array[1].ToString();

        var bindingValue = parser.Scope.GetBinding(name);

        if (bindingValue == null)
        {
            parser.Error("Unknown variable " + name + ". Make sure " + name + " has been bound in an enclosing \"let\" expression before using it.", 1);
            return null;
        }

        return new VarExpression(name, bindingValue);
    }

    public VarExpression(string name, IExpression result)
    {
        Name = name;
        Result = result;
    }

    public string Name { get; }

    IExpression Result;

    public override object? Evaluate(EvaluationContext ctx)
    {
        return Result.Evaluate(ctx);
    }

    public override object? PossibleOutputs()
    {
        return Result.PossibleOutputs(); ;
    }

    public bool Equals(VarExpression? other)
    {
        return other is not null && Name == other.Name;
    }

    public override bool Equals(object? obj) => obj is VarExpression other && Equals(other);

    public override int GetHashCode() => Name.GetHashCode();
}