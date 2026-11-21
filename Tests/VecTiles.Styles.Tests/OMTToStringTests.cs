using System.Text.Json.Nodes;
using VecTiles.Styles.OpenMapTiles.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures
/// to-string.a:
/// ["to-string", ["get", "x"]]
/// to-string.b: differs in the attribute key.
/// </summary>
public class OMTToStringTests
{
    /// <summary>
    /// Verifies that fixture a is structurally equal to a second parse of the same fixture.
    /// </summary>
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = OMTExpressionFixture.Parse("to-string", "a");
        var expressionA2 = OMTExpressionFixture.Parse("to-string", "a");

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
        var expressionA = OMTExpressionFixture.Parse("to-string", "a");
        var expressionB = OMTExpressionFixture.Parse("to-string", "b");

        Assert.NotNull(expressionA);
        Assert.NotNull(expressionB);
        Assert.NotEqual(expressionA, expressionB);
    }

    /// <summary>
    /// Verifies that the fixtures evaluate to the expected results.
    /// </summary>
    [Theory]
    [InlineData(1.5d, "1.5")]
    [InlineData(2d, "2")]
    [InlineData(true, "true")]
    [InlineData(false, "false")]
    [InlineData("raw", "raw")]
    public void FixtureEvaluationTest(object? x, string expected)
    {
        var expression = OMTExpressionFixture.Parse("to-string", "a");

        Assert.NotNull(expression);

        Assert.Equal(expected, expression!.Evaluate(OMTExpressionTestsHelper.CreateContext(("x", x))));
    }
}
