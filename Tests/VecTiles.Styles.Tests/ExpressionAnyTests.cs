using System.Text.Json.Nodes;
using VecTiles.Styles.MapLibre.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures
/// any.a:
/// ["any", ["boolean", ["get", "x"]], ["boolean", ["get", "y"]]]
/// any.b: differs in the second attribute key.
/// </summary>
public class ExpressionAnyTests
{
    /// <summary>
    /// Verifies that fixture a is structurally equal to a second parse of the same fixture.
    /// </summary>
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = ExpressionFixture.Parse("any", "a");
        var expressionA2 = ExpressionFixture.Parse("any", "a");

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
        var expressionA = ExpressionFixture.Parse("any", "a");
        var expressionB = ExpressionFixture.Parse("any", "b");

        Assert.NotNull(expressionA);
        Assert.NotNull(expressionB);
        Assert.NotEqual(expressionA, expressionB);
    }

    /// <summary>
    /// Verifies that the fixtures evaluate to the expected results.
    /// </summary>
    [Theory]
    [InlineData(true, true, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(false, false, false)]
    public void FixtureEvaluationTest(bool x, bool y, bool expected)
    {
        var expression = ExpressionFixture.Parse("any", "a");

        Assert.NotNull(expression);

        var context = ExpressionTestsHelper.CreateContext(("x", x), ("y", y));

        Assert.Equal(expected, expression!.Evaluate(context));
    }

    /// <summary>
    /// Verifies that a failing assertion on a missing attribute makes the result falsy.
    /// </summary>
    [Fact]
    public void MissingAttributeIsFalsyTest()
    {
        var expression = ExpressionFixture.Parse("any", "a");

        Assert.NotNull(expression);

        // All conditions fail on the missing attribute, so the result is falsy
        Assert.False((bool)expression!.Evaluate(ExpressionTestsHelper.CreateContext(("x", false)))!);
    }
}
