using System.Text.Json.Nodes;
using VecTiles.Styles.OpenMapTiles.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures
/// sqrt.a:
/// ["sqrt", ["get", "x"]]
/// sqrt.b: differs in the attribute key.
/// </summary>
public class OMTSqrtTests
{
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = OMTExpressionFixture.Parse("sqrt", "a");
        var expressionA2 = OMTExpressionFixture.Parse("sqrt", "a");

        Assert.NotNull(expressionA1);
        Assert.NotNull(expressionA2);
        Assert.Equal(expressionA1, expressionA2);
    }

    [Fact]
    public void FixtureIsUnequalToOtherFixtureTest()
    {
        var expressionA = OMTExpressionFixture.Parse("sqrt", "a");
        var expressionB = OMTExpressionFixture.Parse("sqrt", "b");

        Assert.NotNull(expressionA);
        Assert.NotNull(expressionB);
        Assert.NotEqual(expressionA, expressionB);
    }

    [Fact]
    public void FixtureEvaluationTest()
    {
        var expression = OMTExpressionFixture.Parse("sqrt", "a");

        Assert.NotNull(expression);

        Assert.Equal(3d, expression!.Evaluate(OMTExpressionTestsHelper.CreateContext(("x", 9d))));
    }
}
