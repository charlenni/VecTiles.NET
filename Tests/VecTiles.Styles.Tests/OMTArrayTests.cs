using System.Text.Json.Nodes;
using VecTiles.Styles.OpenMapTiles.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures
/// array.a:
/// ["array", ["literal", [1, 2, 3]]]
/// array.b: differs in the last array element.
/// </summary>
public class OMTArrayTests
{
    /// <summary>
    /// Verifies that fixture a is structurally equal to a second parse of the same fixture.
    /// </summary>
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = OMTExpressionFixture.Parse("array", "a");
        var expressionA2 = OMTExpressionFixture.Parse("array", "a");

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
        var expressionA = OMTExpressionFixture.Parse("array", "a");
        var expressionB = OMTExpressionFixture.Parse("array", "b");

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
        var expression = OMTExpressionFixture.Parse("array", "a");

        Assert.NotNull(expression);

        Assert.Equal(new object?[] { 1d, 2d, 3d }, expression!.Evaluate(OMTExpressionTestsHelper.CreateContext()));
    }
}
