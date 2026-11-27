using System.Text.Json.Nodes;
using VecTiles.Styles.MapLibre.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures
/// semiliteral.a:
/// ["semiliteral", [["get", "x"], ["get", "y"]]]
/// semiliteral.b: differs in the order of the elements.
/// </summary>
public class ExpressionSemiliteralTests
{
    /// <summary>
    /// Verifies that fixture a is structurally equal to a second parse of the same fixture.
    /// </summary>
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = ExpressionFixture.Parse("semiliteral", "a");
        var expressionA2 = ExpressionFixture.Parse("semiliteral", "a");

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
        var expressionA = ExpressionFixture.Parse("semiliteral", "a");
        var expressionB = ExpressionFixture.Parse("semiliteral", "b");

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
        var expressionA = ExpressionFixture.Parse("semiliteral", "a");
        var expressionB = ExpressionFixture.Parse("semiliteral", "b");

        Assert.NotNull(expressionA);
        Assert.NotNull(expressionB);

        Assert.Equal(new object?[] { "a", "b" }, expressionA!.Evaluate(ExpressionTestsHelper.CreateContext(("x", "a"), ("y", "b"))));
        Assert.Equal(new object?[] { "b", "a" }, expressionB!.Evaluate(ExpressionTestsHelper.CreateContext(("x", "a"), ("y", "b"))));
    }
}
