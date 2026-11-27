using VecTiles.Common.Primitives;
using System.Text.Json.Nodes;
using VecTiles.Styles.MapLibre.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures
/// rgba.a:
/// ["rgba", 0, 0, 255, 1]
/// rgba.b: differs in the alpha channel.
/// </summary>
public class ExpressionRgbaTests
{
    /// <summary>
    /// Verifies that fixture a is structurally equal to a second parse of the same fixture.
    /// </summary>
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = ExpressionFixture.Parse("rgba", "a");
        var expressionA2 = ExpressionFixture.Parse("rgba", "a");

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
        var expressionA = ExpressionFixture.Parse("rgba", "a");
        var expressionB = ExpressionFixture.Parse("rgba", "b");

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
        var colorA = Assert.IsType<Color>(ExpressionFixture.Parse("rgba", "a")!.Evaluate(ExpressionTestsHelper.CreateContext()));
        var colorB = Assert.IsType<Color>(ExpressionFixture.Parse("rgba", "b")!.Evaluate(ExpressionTestsHelper.CreateContext()));

        Assert.Equal(new Color(0, 0, 255, 255), colorA);
        Assert.Equal(new Color(0, 0, 255, 128), colorB);
    }
}
