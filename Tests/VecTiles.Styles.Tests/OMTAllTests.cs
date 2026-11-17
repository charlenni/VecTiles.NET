using Newtonsoft.Json.Linq;
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
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = OMTExpressionFixture.Parse("all", "a");
        var expressionA2 = OMTExpressionFixture.Parse("all", "a");

        Assert.NotNull(expressionA1);
        Assert.NotNull(expressionA2);
        Assert.Equal(expressionA1, expressionA2);
    }

    [Fact]
    public void FixtureIsUnequalToOtherFixtureTest()
    {
        var expressionA = OMTExpressionFixture.Parse("all", "a");
        var expressionB = OMTExpressionFixture.Parse("all", "b");

        Assert.NotNull(expressionA);
        Assert.NotNull(expressionB);
        Assert.NotEqual(expressionA, expressionB);
    }

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

    [Fact]
    public void MissingAttributeIsFalsyTest()
    {
        var expression = OMTExpressionFixture.Parse("all", "a");

        Assert.NotNull(expression);

        // The boolean assertion of a missing attribute fails, so the result is falsy
        Assert.False((bool)expression!.Evaluate(OMTExpressionTestsHelper.CreateContext(("x", true)))!);
    }
}
