using Newtonsoft.Json.Linq;
using VecTiles.Styles.OpenMapTiles.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures
/// image.a:
/// ["image", "maki-11"]
/// image.b: differs in the image name.
/// </summary>
public class OMTImageTests
{
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = OMTExpressionFixture.Parse("image", "a");
        var expressionA2 = OMTExpressionFixture.Parse("image", "a");

        Assert.NotNull(expressionA1);
        Assert.NotNull(expressionA2);
        Assert.Equal(expressionA1, expressionA2);
    }

    [Fact]
    public void FixtureIsUnequalToOtherFixtureTest()
    {
        var expressionA = OMTExpressionFixture.Parse("image", "a");
        var expressionB = OMTExpressionFixture.Parse("image", "b");

        Assert.NotNull(expressionA);
        Assert.NotNull(expressionB);
        Assert.NotEqual(expressionA, expressionB);
    }

    [Fact]
    public void FixtureEvaluationTest()
    {
        Assert.Equal("maki-11", OMTExpressionFixture.Parse("image", "a")!.Evaluate(OMTExpressionTestsHelper.CreateContext()));
    }
}
