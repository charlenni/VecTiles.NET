using System.Text.Json.Nodes;
using VecTiles.Styles.MapLibre.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures
/// number.a:
/// ["number", ["get", "x"]]
/// number.b: differs in the attribute key.
/// </summary>
public class ExpressionNumberTests
{
    /// <summary>
    /// Verifies that fixture a is structurally equal to a second parse of the same fixture.
    /// </summary>
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = ExpressionFixture.Parse("number", "a");
        var expressionA2 = ExpressionFixture.Parse("number", "a");

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
        var expressionA = ExpressionFixture.Parse("number", "a");
        var expressionB = ExpressionFixture.Parse("number", "b");

        Assert.NotNull(expressionA);
        Assert.NotNull(expressionB);
        Assert.NotEqual(expressionA, expressionB);
    }

    /// <summary>
    /// Verifies that the fixtures evaluate to the expected results.
    /// </summary>
    [Fact]
    public void FixtureEvaluationTest()
    {
        var expression = ExpressionFixture.Parse("number", "a");

        Assert.NotNull(expression);

        Assert.Equal(42.5d, expression!.Evaluate(ExpressionTestsHelper.CreateContext(("x", 42.5d))));
    }

    /// <summary>
    /// Verifies that the number assertion falls back to further operands and finally to the default value.
    /// </summary>
    [Fact]
    public void FallbackTest()
    {
        var expression = ExpressionParser.ParseExpression(JsonNode.Parse("[\"number\", [\"get\", \"x\"], [\"number\", [\"get\", \"y\"]], 0]"));

        Assert.NotNull(expression);

        Assert.Equal(3d, expression!.Evaluate(ExpressionTestsHelper.CreateContext(("y", 3d))));
        Assert.Equal(0d, expression!.Evaluate(ExpressionTestsHelper.CreateContext()));
    }
}
