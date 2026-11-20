using System.Text.Json.Nodes;
using VecTiles.Styles.OpenMapTiles.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native "get" fixtures
/// (test/fixtures/expression_equality/get.json):
/// get.a: ["number", ["get", "x"]]
/// get.b: differs in the attribute key ("x" vs. "x_other").
/// The "number" assertion isn't implemented yet, so the fixtures are
/// stripped down to the inner "get" expression.
/// </summary>
public class OMTExpressionGetTests
{
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = ExpressionParser.ParseExpression(JsonNode.Parse("""["get", "x"]"""));
        var expressionA2 = ExpressionParser.ParseExpression(JsonNode.Parse("""["get", "x"]"""));

        Assert.NotNull(expressionA1);
        Assert.NotNull(expressionA2);
        Assert.Equal(expressionA1, expressionA2);
    }

    [Fact]
    public void FixtureIsUnequalToOtherFixtureTest()
    {
        // The leaf difference of the fixture: "x" vs. "x_other"
        var expressionA = ExpressionParser.ParseExpression(JsonNode.Parse("""["get", "x"]"""));
        var expressionB = ExpressionParser.ParseExpression(JsonNode.Parse("""["get", "x_other"]"""));

        Assert.NotNull(expressionA);
        Assert.NotNull(expressionB);
        Assert.NotEqual(expressionA, expressionB);
    }

    [Fact]
    public void FixtureEvaluationTest()
    {
        var expression = ExpressionParser.ParseExpression(JsonNode.Parse("""["get", "x"]"""));

        Assert.NotNull(expression);

        var context = OMTExpressionTestsHelper.CreateContext(("x", "value"));

        Assert.Equal("value", expression.Evaluate(context));
    }

    [Fact]
    public void FixtureEvaluatesToNullOnMissingAttributeTest()
    {
        var expression = ExpressionParser.ParseExpression(JsonNode.Parse("""["get", "x_other"]"""));

        Assert.NotNull(expression);

        var context = OMTExpressionTestsHelper.CreateContext(("x", "value"));

        Assert.Null(expression.Evaluate(context));
    }

    [Fact]
    public void GetRequiresStringLiteralKeyTest()
    {
        var invalid = ExpressionParser.ParseExpression(JsonNode.Parse("""["get", 42]"""));

        Assert.Null(invalid);
    }
}
