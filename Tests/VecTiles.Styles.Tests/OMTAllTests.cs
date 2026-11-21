using System.Text.Json.Nodes;
using VecTiles.Styles.OpenMapTiles.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures
/// all.a:
/// ["all", ["boolean", ["get", "x"]], ["boolean", ["get", "y"]]]
/// all.b: differs in the second attribute key.
/// </summary>
public class OMTAllTests
{
    /// <summary>
    /// Verifies that fixture a is structurally equal to a second parse of the same fixture.
    /// </summary>
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = OMTExpressionFixture.Parse("all", "a");
        var expressionA2 = OMTExpressionFixture.Parse("all", "a");

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
        var expressionA = OMTExpressionFixture.Parse("all", "a");
        var expressionB = OMTExpressionFixture.Parse("all", "b");

        Assert.NotNull(expressionA);
        Assert.NotNull(expressionB);
        Assert.NotEqual(expressionA, expressionB);
    }

    /// <summary>
    /// Verifies that the fixtures evaluate to the expected results.
    /// </summary>
    [Theory]
    [InlineData(true, true, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(false, false, false)]
    public void FixtureEvaluationTest(bool x, bool y, bool expected)
    {
        var expression = OMTExpressionFixture.Parse("all", "a");

        Assert.NotNull(expression);

        var context = OMTExpressionTestsHelper.CreateContext(("x", x), ("y", y));

        Assert.Equal(expected, expression!.Evaluate(context));
    }

    /// <summary>
    /// Verifies that a failing assertion on a missing attribute makes the result falsy.
    /// </summary>
    [Fact]
    public void MissingAttributeIsFalsyTest()
    {
        var expression = OMTExpressionFixture.Parse("all", "a");

        Assert.NotNull(expression);

        // The boolean assertion of a missing attribute fails, so the result is falsy
        Assert.False((bool)expression!.Evaluate(OMTExpressionTestsHelper.CreateContext(("x", true)))!);
    }
}
