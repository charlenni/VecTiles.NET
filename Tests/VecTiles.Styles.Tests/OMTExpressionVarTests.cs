using Newtonsoft.Json.Linq;
using VecTiles.Styles.OpenMapTiles.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests for the "var" expression analogous to the MapLibre Native let/var
/// fixtures (test/fixtures/expression_equality/let.json): the fixture bodies
/// use "+", which isn't implemented yet, so this covers the variable lookup
/// itself.
/// </summary>
public class OMTExpressionVarTests
{
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = ExpressionParser.ParseExpression(JToken.Parse("""["var", "a"]"""));
        var expressionA2 = ExpressionParser.ParseExpression(JToken.Parse("""["var", "a"]"""));

        // "var" without an enclosing "let" fails to parse
        Assert.Null(expressionA1);
        Assert.Null(expressionA2);
    }

    [Fact]
    public void FixtureEvaluationTest()
    {
        var expression = ExpressionParser.ParseExpression(JToken.Parse(
            """
            ["let", "x", "Apple", ["var", "x"]]
            """));

        Assert.NotNull(expression);

        Assert.Equal("Apple", expression!.Evaluate(OMTExpressionTestsHelper.CreateContext()));
    }

    [Fact]
    public void UnknownVariableTest()
    {
        var expression = ExpressionParser.ParseExpression(JToken.Parse(
            """
            ["let", "x", "Apple", ["var", "y"]]
            """));

        Assert.Null(expression);
    }
}
