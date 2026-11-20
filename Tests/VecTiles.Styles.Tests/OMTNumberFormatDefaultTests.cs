using System.Text.Json.Nodes;
using VecTiles.Styles.OpenMapTiles.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures
/// number-format-default.a:
/// ["number-format", 123456.789, {}]
/// number-format-default.b: differs in the sign of the number.
/// </summary>
public class OMTNumberFormatDefaultTests
{
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = OMTExpressionFixture.Parse("number-format-default", "a");
        var expressionA2 = OMTExpressionFixture.Parse("number-format-default", "a");

        Assert.NotNull(expressionA1);
        Assert.NotNull(expressionA2);
        Assert.Equal(expressionA1, expressionA2);
    }

    [Fact]
    public void FixtureIsUnequalToOtherFixtureTest()
    {
        var expressionA = OMTExpressionFixture.Parse("number-format-default", "a");
        var expressionB = OMTExpressionFixture.Parse("number-format-default", "b");

        Assert.NotNull(expressionA);
        Assert.NotNull(expressionB);
        Assert.NotEqual(expressionA, expressionB);
    }

    [Fact]
    public void FixtureEvaluationTest()
    {
        var expressionA = OMTExpressionFixture.Parse("number-format-default", "a");
        var expressionB = OMTExpressionFixture.Parse("number-format-default", "b");

        Assert.NotNull(expressionA);
        Assert.NotNull(expressionB);

        Assert.Equal("123456.789", expressionA!.Evaluate(OMTExpressionTestsHelper.CreateContext()));
        Assert.Equal("-123456.789", expressionB!.Evaluate(OMTExpressionTestsHelper.CreateContext()));
    }
}
