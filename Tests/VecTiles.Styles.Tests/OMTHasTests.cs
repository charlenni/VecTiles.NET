using Newtonsoft.Json.Linq;
using VecTiles.Styles.OpenMapTiles.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures
/// has.a:
/// ["has", "x"]
/// has.b: differs in the attribute key.
/// </summary>
public class OMTHasTests
{
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = OMTExpressionFixture.Parse("has", "a");
        var expressionA2 = OMTExpressionFixture.Parse("has", "a");

        Assert.NotNull(expressionA1);
        Assert.NotNull(expressionA2);
        Assert.Equal(expressionA1, expressionA2);
    }

    [Fact]
    public void FixtureIsUnequalToOtherFixtureTest()
    {
        var expressionA = OMTExpressionFixture.Parse("has", "a");
        var expressionB = OMTExpressionFixture.Parse("has", "b");

        Assert.NotNull(expressionA);
        Assert.NotNull(expressionB);
        Assert.NotEqual(expressionA, expressionB);
    }

    [Fact]
    public void FixtureEvaluationTest()
    {
        var expression = OMTExpressionFixture.Parse("has", "a");

        Assert.NotNull(expression);

        Assert.Equal(true, expression!.Evaluate(OMTExpressionTestsHelper.CreateContext(("x", true))));
    }

    [Theory]
    [InlineData("x", true)]
    [InlineData("x", null)]
    [InlineData("y_other", true)]
    public void AttributeExistenceTest(string key, object? value)
    {
        var expression = OMTExpressionFixture.Parse("has", "a");

        Assert.NotNull(expression);

        Assert.Equal(key == "x", expression!.Evaluate(OMTExpressionTestsHelper.CreateContext((key, value))));
    }
}
