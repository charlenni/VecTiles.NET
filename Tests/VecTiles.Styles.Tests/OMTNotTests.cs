using System.Text.Json.Nodes;
using VecTiles.Styles.OpenMapTiles.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures
/// not.a:
/// ["!", ["boolean", ["get", "x"]]]
/// not.b: differs in the attribute key.
/// </summary>
public class OMTNotTests
{
    /// <summary>
    /// Verifies that fixture a is structurally equal to a second parse of the same fixture.
    /// </summary>
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = OMTExpressionFixture.Parse("not", "a");
        var expressionA2 = OMTExpressionFixture.Parse("not", "a");

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
        var expressionA = OMTExpressionFixture.Parse("not", "a");
        var expressionB = OMTExpressionFixture.Parse("not", "b");

        Assert.NotNull(expressionA);
        Assert.NotNull(expressionB);
        Assert.NotEqual(expressionA, expressionB);
    }

    /// <summary>
    /// Verifies that the fixtures evaluate to the expected results.
    /// </summary>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void FixtureEvaluationTest(bool x, bool expected)
    {
        var expression = OMTExpressionFixture.Parse("not", "a");

        Assert.NotNull(expression);

        Assert.Equal(expected, expression!.Evaluate(OMTExpressionTestsHelper.CreateContext(("x", x))));
    }
}
