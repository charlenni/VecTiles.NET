using System.Text.Json.Nodes;
using VecTiles.Styles.MapLibre.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures
/// min.a:
/// ["min", ["get", "x"], 0]
/// min.b: differs in the second number.
/// </summary>
public class ExpressionMinTests
{
    /// <summary>
    /// Verifies that fixture a is structurally equal to a second parse of the same fixture.
    /// </summary>
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = ExpressionFixture.Parse("min", "a");
        var expressionA2 = ExpressionFixture.Parse("min", "a");

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
        var expressionA = ExpressionFixture.Parse("min", "a");
        var expressionB = ExpressionFixture.Parse("min", "b");

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
        var expression = ExpressionFixture.Parse("min", "a");

        Assert.NotNull(expression);

        Assert.Equal(-5d, expression!.Evaluate(ExpressionTestsHelper.CreateContext(("x", -5d))));
    }

    /// <summary>
    /// Verifies that a missing attribute that cannot be compared numerically evaluates to null.
    /// </summary>
    [Fact]
    public void MissingAttributeTest()
    {
        var expression = ExpressionFixture.Parse("min", "a");

        Assert.NotNull(expression);

        // A missing attribute can't be compared numerically
        Assert.Null(expression!.Evaluate(ExpressionTestsHelper.CreateContext()));
    }
}
