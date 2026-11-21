using System.Text.Json.Nodes;
using VecTiles.Styles.OpenMapTiles.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures
/// downcase.a:
/// ["downcase", "StRiNg"]
/// downcase.b: differs in the string.
/// </summary>
public class OMTDowncaseTests
{
    /// <summary>
    /// Verifies that fixture a is structurally equal to a second parse of the same fixture.
    /// </summary>
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = OMTExpressionFixture.Parse("downcase", "a");
        var expressionA2 = OMTExpressionFixture.Parse("downcase", "a");

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
        var expressionA = OMTExpressionFixture.Parse("downcase", "a");
        var expressionB = OMTExpressionFixture.Parse("downcase", "b");

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
        Assert.Equal("string", OMTExpressionFixture.Parse("downcase", "a")!.Evaluate(OMTExpressionTestsHelper.CreateContext()));
        Assert.Equal("string_other", OMTExpressionFixture.Parse("downcase", "b")!.Evaluate(OMTExpressionTestsHelper.CreateContext()));
    }
}
