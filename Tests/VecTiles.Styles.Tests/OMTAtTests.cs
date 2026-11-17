using Newtonsoft.Json.Linq;
using VecTiles.Styles.OpenMapTiles.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures
/// at.a:
/// ["number", ["at", ["number", ["get", "i"]], ["array", ["get", "arr"]]]]
/// at.b: differs in the array attribute key.
/// </summary>
public class OMTAtTests
{
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = OMTExpressionFixture.Parse("at", "a");
        var expressionA2 = OMTExpressionFixture.Parse("at", "a");

        Assert.NotNull(expressionA1);
        Assert.NotNull(expressionA2);
        Assert.Equal(expressionA1, expressionA2);
    }

    [Fact]
    public void FixtureIsUnequalToOtherFixtureTest()
    {
        var expressionA = OMTExpressionFixture.Parse("at", "a");
        var expressionB = OMTExpressionFixture.Parse("at", "b");

        Assert.NotNull(expressionA);
        Assert.NotNull(expressionB);
        Assert.NotEqual(expressionA, expressionB);
    }

    [Fact]
    public void FixtureEvaluationTest()
    {
        var expression = OMTExpressionFixture.Parse("at", "a");

        Assert.NotNull(expression);

        var context = OMTExpressionTestsHelper.CreateContext(("i", 1d), ("arr", new object?[] { 10d, 20d, 30d }));

        Assert.Equal(20d, expression!.Evaluate(context));
    }

    [Fact]
    public void OutOfRangeIndexTest()
    {
        var expression = OMTExpressionFixture.Parse("at", "a");

        Assert.NotNull(expression);

        var context = OMTExpressionTestsHelper.CreateContext(("i", 7d), ("arr", new object?[] { 10d, 20d, 30d }));

        Assert.Null(expression!.Evaluate(context));
    }
}
