using VecTiles.Common.Primitives;
using System.Text.Json.Nodes;
using VecTiles.Styles.OpenMapTiles.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures
/// rgba.a:
/// ["rgba", 0, 0, 255, 1]
/// rgba.b: differs in the alpha channel.
/// </summary>
public class OMTRgbaTests
{
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = OMTExpressionFixture.Parse("rgba", "a");
        var expressionA2 = OMTExpressionFixture.Parse("rgba", "a");

        Assert.NotNull(expressionA1);
        Assert.NotNull(expressionA2);
        Assert.Equal(expressionA1, expressionA2);
    }

    [Fact]
    public void FixtureIsUnequalToOtherFixtureTest()
    {
        var expressionA = OMTExpressionFixture.Parse("rgba", "a");
        var expressionB = OMTExpressionFixture.Parse("rgba", "b");

        Assert.NotNull(expressionA);
        Assert.NotNull(expressionB);
        Assert.NotEqual(expressionA, expressionB);
    }

    [Fact]
    public void FixtureEvaluationTest()
    {
        var colorA = Assert.IsType<Color>(OMTExpressionFixture.Parse("rgba", "a")!.Evaluate(OMTExpressionTestsHelper.CreateContext()));
        var colorB = Assert.IsType<Color>(OMTExpressionFixture.Parse("rgba", "b")!.Evaluate(OMTExpressionTestsHelper.CreateContext()));

        Assert.Equal(new Color(0, 0, 255, 255), colorA);
        Assert.Equal(new Color(0, 0, 255, 128), colorB);
    }
}
