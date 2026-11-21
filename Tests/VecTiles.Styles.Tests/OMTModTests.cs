using System.Text.Json.Nodes;
using VecTiles.Styles.OpenMapTiles.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures
/// mod.a:
/// ["%", 18, 12]
/// mod.b: differs in the divisor.
/// </summary>
public class OMTModTests
{
    /// <summary>
    /// Verifies that fixture a is structurally equal to a second parse of the same fixture.
    /// </summary>
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = OMTExpressionFixture.Parse("mod", "a");
        var expressionA2 = OMTExpressionFixture.Parse("mod", "a");

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
        var expressionA = OMTExpressionFixture.Parse("mod", "a");
        var expressionB = OMTExpressionFixture.Parse("mod", "b");

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
        Assert.Equal(6d, OMTExpressionFixture.Parse("mod", "a")!.Evaluate(OMTExpressionTestsHelper.CreateContext()));
        Assert.Equal(5d, OMTExpressionFixture.Parse("mod", "b")!.Evaluate(OMTExpressionTestsHelper.CreateContext()));
    }
}
