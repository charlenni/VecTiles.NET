using System.Text.Json.Nodes;
using VecTiles.Styles.MapLibre.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures
/// minus.a:
/// ["-", 5, 7]
/// minus.b: differs in the subtrahend.
/// </summary>
public class ExpressionMinusTests
{
    /// <summary>
    /// Verifies that fixture a is structurally equal to a second parse of the same fixture.
    /// </summary>
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = ExpressionFixture.Parse("minus", "a");
        var expressionA2 = ExpressionFixture.Parse("minus", "a");

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
        var expressionA = ExpressionFixture.Parse("minus", "a");
        var expressionB = ExpressionFixture.Parse("minus", "b");

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
        Assert.Equal(-2d, ExpressionFixture.Parse("minus", "a")!.Evaluate(ExpressionTestsHelper.CreateContext()));
    }

    /// <summary>
    /// Verifies that the minus expression with a single operand negates the value.
    /// </summary>
    [Fact]
    public void NegationTest()
    {
        var expression = ExpressionParser.ParseExpression(JsonNode.Parse("[\"-\", [\"number\", [\"get\", \"x\"]]]"));

        Assert.NotNull(expression);

        Assert.Equal(-3.5d, expression!.Evaluate(ExpressionTestsHelper.CreateContext(("x", 3.5d))));
    }
}
