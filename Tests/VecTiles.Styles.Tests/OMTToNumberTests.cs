using System.Text.Json.Nodes;
using VecTiles.Styles.OpenMapTiles.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures
/// to-number.a:
/// ["to-number", ["get", "x"]]
/// to-number.b: differs in the attribute key.
/// </summary>
public class OMTToNumberTests
{
    /// <summary>
    /// Verifies that fixture a is structurally equal to a second parse of the same fixture.
    /// </summary>
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = OMTExpressionFixture.Parse("to-number", "a");
        var expressionA2 = OMTExpressionFixture.Parse("to-number", "a");

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
        var expressionA = OMTExpressionFixture.Parse("to-number", "a");
        var expressionB = OMTExpressionFixture.Parse("to-number", "b");

        Assert.NotNull(expressionA);
        Assert.NotNull(expressionB);
        Assert.NotEqual(expressionA, expressionB);
    }

    /// <summary>
    /// Verifies that the fixtures evaluate to the expected results.
    /// </summary>
    [Theory]
    [InlineData(1.5d, 1.5d)]
    [InlineData(true, 1d)]
    [InlineData(false, 0d)]
    [InlineData("42.5", 42.5d)]
    public void FixtureEvaluationTest(object? x, double expected)
    {
        var expression = OMTExpressionFixture.Parse("to-number", "a");

        Assert.NotNull(expression);

        Assert.Equal(expected, expression!.Evaluate(OMTExpressionTestsHelper.CreateContext(("x", x))));
    }

    /// <summary>
    /// Verifies that a missing attribute is coerced to 0, like in MapLibre.
    /// </summary>
    [Fact]
    public void MissingAttributeTest()
    {
        var expression = OMTExpressionFixture.Parse("to-number", "a");

        Assert.NotNull(expression);

        // A missing attribute is coerced to 0, like in MapLibre
        Assert.Equal(0d, expression!.Evaluate(OMTExpressionTestsHelper.CreateContext()));
    }
}
