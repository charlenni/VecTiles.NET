using System.Text.Json.Nodes;
using VecTiles.Styles.OpenMapTiles.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native "match" fixtures
/// (test/fixtures/expression_equality/match.json):
/// match.a:
/// ["match", ["get", "x"], "a", "Apple", "b", "Banana", "Kumquat"]
/// match.b: differs in the fallback ("Kumquat" vs. "Kumquat_other").
/// </summary>
public class OMTExpressionMatchTests
{
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = OMTExpressionFixture.Parse("match", "a");
        var expressionA2 = OMTExpressionFixture.Parse("match", "a");

        Assert.NotNull(expressionA1);
        Assert.NotNull(expressionA2);
        Assert.Equal(expressionA1, expressionA2);
    }

    [Fact]
    public void FixtureIsUnequalToOtherFixtureTest()
    {
        var expressionA = OMTExpressionFixture.Parse("match", "a");
        var expressionB = OMTExpressionFixture.Parse("match", "b");

        Assert.NotNull(expressionA);
        Assert.NotNull(expressionB);
        Assert.NotEqual(expressionA, expressionB);
    }

    [Theory]
    [InlineData("a", "Apple")]
    [InlineData("b", "Banana")]
    [InlineData("c", "Kumquat")]
    public void FixtureEvaluationTest(string x, string expected)
    {
        var expression = OMTExpressionFixture.Parse("match", "a");

        Assert.NotNull(expression);

        var context = OMTExpressionTestsHelper.CreateContext(("x", x));

        Assert.Equal(expected, expression.Evaluate(context));
    }

    [Fact]
    public void FixtureFallsBackOnMissingAttributeTest()
    {
        var expression = OMTExpressionFixture.Parse("match", "a");

        Assert.NotNull(expression);

        var context = OMTExpressionTestsHelper.CreateContext();

        Assert.Equal("Kumquat", expression.Evaluate(context));
    }

    [Fact]
    public void LabelListTest()
    {
        var expression = ExpressionParser.ParseExpression(JsonNode.Parse(
            """
            ["match", ["get", "x"], ["a", "b"], "Apple", "Kumquat"]
            """));

        Assert.NotNull(expression);

        Assert.Equal("Apple", expression!.Evaluate(OMTExpressionTestsHelper.CreateContext(("x", "a"))));
        Assert.Equal("Apple", expression.Evaluate(OMTExpressionTestsHelper.CreateContext(("x", "b"))));
        Assert.Equal("Kumquat", expression.Evaluate(OMTExpressionTestsHelper.CreateContext(("x", "c"))));
    }
}
