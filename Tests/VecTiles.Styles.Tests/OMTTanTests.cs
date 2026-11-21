using System.Text.Json.Nodes;
using VecTiles.Styles.OpenMapTiles.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures
/// tan.a:
/// ["tan", 0.7853981633974483]
/// tan.b: differs in the angle.
/// </summary>
public class OMTTanTests
{
    /// <summary>
    /// Verifies that fixture a is structurally equal to a second parse of the same fixture.
    /// </summary>
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = OMTExpressionFixture.Parse("tan", "a");
        var expressionA2 = OMTExpressionFixture.Parse("tan", "a");

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
        var expressionA = OMTExpressionFixture.Parse("tan", "a");
        var expressionB = OMTExpressionFixture.Parse("tan", "b");

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
        Assert.Equal(1d, (double)OMTExpressionFixture.Parse("tan", "a")!.Evaluate(OMTExpressionTestsHelper.CreateContext())!, 12);
        Assert.Equal(-4.5880378249839007, (double)OMTExpressionFixture.Parse("tan", "b")!.Evaluate(OMTExpressionTestsHelper.CreateContext())!, 12);
    }
}
