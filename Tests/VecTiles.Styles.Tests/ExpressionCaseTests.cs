using System.Text.Json.Nodes;
using VecTiles.Styles.MapLibre.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native "case" fixtures
/// (test/fixtures/expression_equality/case.json):
/// case.a:
/// ["case", ["get", "x"], "x", ["get", "y"], "y", "otherwise"]
/// case.b: differs in the fallback ("otherwise" vs. "otherwise_other").
/// </summary>
public class ExpressionCaseTests
{
    /// <summary>
    /// Verifies that fixture a is structurally equal to a second parse of the same fixture.
    /// </summary>
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = ExpressionFixture.Parse("case", "a");
        var expressionA2 = ExpressionFixture.Parse("case", "a");

        Assert.NotNull(expressionA1);
        Assert.NotNull(expressionA2);
        Assert.Equal(expressionA1, expressionA2);
    }

    /// <summary>
    /// Verifies that fixture a is not structurally equal to fixture b, which differs in exactly one leaf.
    /// </summary>
    [Fact]
    public void FixtureIsUnequalToOtherFixtureTest()
    {
        var expressionA = ExpressionFixture.Parse("case", "a");
        var expressionB = ExpressionFixture.Parse("case", "b");

        Assert.NotNull(expressionA);
        Assert.NotNull(expressionB);
        Assert.NotEqual(expressionA, expressionB);
    }

    /// <summary>
    /// Verifies that the fixtures evaluate to the expected results.
    /// </summary>
    [Theory]
    [InlineData(true, true, "x")]
    [InlineData(true, false, "x")]
    [InlineData(false, true, "y")]
    public void FixtureEvaluationTest(bool x, bool y, string expected)
    {
        var expression = ExpressionFixture.Parse("case", "a");

        Assert.NotNull(expression);

        var context = ExpressionTestsHelper.CreateContext(("x", x), ("y", y));

        Assert.Equal(expected, expression.Evaluate(context));
    }

    /// <summary>
    /// Verifies that the case fixture falls back to the otherwise output when all conditions are falsy.
    /// </summary>
    [Fact]
    public void FixtureFallsBackOnFalsyConditionsTest()
    {
        var expressionA = ExpressionFixture.Parse("case", "a");
        var expressionB = ExpressionFixture.Parse("case", "b");

        Assert.NotNull(expressionA);
        Assert.NotNull(expressionB);

        var context = ExpressionTestsHelper.CreateContext(("x", false));

        Assert.Equal("otherwise", expressionA.Evaluate(context));
        Assert.Equal("otherwise_other", expressionB.Evaluate(context));
    }

    /// <summary>
    /// Verifies that a case expression without a fallback output fails to parse.
    /// </summary>
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
