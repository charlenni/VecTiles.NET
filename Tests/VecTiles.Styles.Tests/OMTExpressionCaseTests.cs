using System.Text.Json.Nodes;
using VecTiles.Styles.OpenMapTiles.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native "case" fixtures
/// (test/fixtures/expression_equality/case.json):
/// case.a:
/// ["case", ["get", "x"], "x", ["get", "y"], "y", "otherwise"]
/// case.b: differs in the fallback ("otherwise" vs. "otherwise_other").
/// </summary>
public class OMTExpressionCaseTests
{
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = OMTExpressionFixture.Parse("case", "a");
        var expressionA2 = OMTExpressionFixture.Parse("case", "a");

        Assert.NotNull(expressionA1);
        Assert.NotNull(expressionA2);
        Assert.Equal(expressionA1, expressionA2);
    }

    [Fact]
    public void FixtureIsUnequalToOtherFixtureTest()
    {
        var expressionA = OMTExpressionFixture.Parse("case", "a");
        var expressionB = OMTExpressionFixture.Parse("case", "b");

        Assert.NotNull(expressionA);
        Assert.NotNull(expressionB);
        Assert.NotEqual(expressionA, expressionB);
    }

    [Theory]
    [InlineData(true, true, "x")]
    [InlineData(true, false, "x")]
    [InlineData(false, true, "y")]
    public void FixtureEvaluationTest(bool x, bool y, string expected)
    {
        var expression = OMTExpressionFixture.Parse("case", "a");

        Assert.NotNull(expression);

        var context = OMTExpressionTestsHelper.CreateContext(("x", x), ("y", y));

        Assert.Equal(expected, expression.Evaluate(context));
    }

    [Fact]
    public void FixtureFallsBackOnFalsyConditionsTest()
    {
        var expressionA = OMTExpressionFixture.Parse("case", "a");
        var expressionB = OMTExpressionFixture.Parse("case", "b");

        Assert.NotNull(expressionA);
        Assert.NotNull(expressionB);

        var context = OMTExpressionTestsHelper.CreateContext(("x", false));

        Assert.Equal("otherwise", expressionA.Evaluate(context));
        Assert.Equal("otherwise_other", expressionB.Evaluate(context));
    }

    [Fact]
    public void CaseRequiresPairsAndFallbackTest()
    {
        // No fallback output
        var invalid = ExpressionParser.ParseExpression(JsonNode.Parse(
            """
            ["case", ["get", "x"], "x"]
            """));

        Assert.Null(invalid);
    }
}
