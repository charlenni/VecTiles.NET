using System.Text.Json.Nodes;
using VecTiles.Styles.MapLibre.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native "match" fixtures
/// (test/fixtures/expression_equality/match.json):
/// match.a:
/// ["match", ["get", "x"], "a", "Apple", "b", "Banana", "Kumquat"]
/// match.b: differs in the fallback ("Kumquat" vs. "Kumquat_other").
/// </summary>
public class ExpressionMatchTests
{
    /// <summary>
    /// Verifies that fixture a is structurally equal to a second parse of the same fixture.
    /// </summary>
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = ExpressionFixture.Parse("match", "a");
        var expressionA2 = ExpressionFixture.Parse("match", "a");

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
        var expressionA = ExpressionFixture.Parse("match", "a");
        var expressionB = ExpressionFixture.Parse("match", "b");

        Assert.NotNull(expressionA);
        Assert.NotNull(expressionB);
        Assert.NotEqual(expressionA, expressionB);
    }

    /// <summary>
    /// Verifies that the fixtures evaluate to the expected results.
    /// </summary>
    [Theory]
    [InlineData("a", "Apple")]
    [InlineData("b", "Banana")]
    [InlineData("c", "Kumquat")]
    public void FixtureEvaluationTest(string x, string expected)
    {
        var expression = ExpressionFixture.Parse("match", "a");

        Assert.NotNull(expression);

        var context = ExpressionTestsHelper.CreateContext(("x", x));

        Assert.Equal(expected, expression.Evaluate(context));
    }

    /// <summary>
    /// Verifies that the match fixture falls back to the default output when the attribute is missing.
    /// </summary>
    [Fact]
    public void FixtureFallsBackOnMissingAttributeTest()
    {
        var expression = ExpressionFixture.Parse("match", "a");

        Assert.NotNull(expression);

        var context = ExpressionTestsHelper.CreateContext();

        Assert.Equal("Kumquat", expression.Evaluate(context));
    }

    /// <summary>
    /// Verifies that the match expression matches input values against label lists.
    /// </summary>
    [Fact]
    public void LabelListTest()
    {
        var expression = ExpressionParser.ParseExpression(JsonNode.Parse(
            """
            ["match", ["get", "x"], ["a", "b"], "Apple", "Kumquat"]
            """));

        Assert.NotNull(expression);

        Assert.Equal("Apple", expression!.Evaluate(ExpressionTestsHelper.CreateContext(("x", "a"))));
        Assert.Equal("Apple", expression.Evaluate(ExpressionTestsHelper.CreateContext(("x", "b"))));
        Assert.Equal("Kumquat", expression.Evaluate(ExpressionTestsHelper.CreateContext(("x", "c"))));
    }
}
