using NetTopologySuite.Geometries;
using System.Text.Json.Nodes;
using VecTiles.Styles.OpenMapTiles.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures
/// within.a:
/// ["within", { "type": "Polygon", "coordinates": [[[0, 0], [0, 5], [5, 5], [5, 0], [0, 0]]] }]
/// within.b: differs in the y coordinate of the second vertex.
/// </summary>
public class OMTWithinTests
{
    /// <summary>
    /// Verifies that fixture a is structurally equal to a second parse of the same fixture.
    /// </summary>
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = OMTExpressionFixture.Parse("within", "a");
        var expressionA2 = OMTExpressionFixture.Parse("within", "a");

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
        var expressionA = OMTExpressionFixture.Parse("within", "a");
        var expressionB = OMTExpressionFixture.Parse("within", "b");

        Assert.NotNull(expressionA);
        Assert.NotNull(expressionB);
        Assert.NotEqual(expressionA, expressionB);
    }

    /// <summary>
    /// Verifies that a point inside the fixture polygon evaluates to true.
    /// </summary>
    [Fact]
    public void PointInsidePolygonTest()
    {
        var expression = OMTExpressionFixture.Parse("within", "a");

        Assert.NotNull(expression);

        Assert.Equal(true, expression!.Evaluate(OMTExpressionTestsHelper.CreateContext(new Point(1, 1))));
    }

    /// <summary>
    /// Verifies that a point outside the fixture polygon evaluates to false.
    /// </summary>
    [Fact]
    public void PointOutsidePolygonTest()
    {
        var expression = OMTExpressionFixture.Parse("within", "a");

        Assert.NotNull(expression);

        Assert.Equal(false, expression!.Evaluate(OMTExpressionTestsHelper.CreateContext(new Point(7, 7))));
    }

    /// <summary>
    /// Verifies that a point on the polygon boundary counts as within.
    /// </summary>
    [Fact]
    public void PointOnBoundaryTest()
    {
        var expression = OMTExpressionFixture.Parse("within", "a");

        Assert.NotNull(expression);

        // Points on the boundary count as within
        Assert.Equal(true, expression!.Evaluate(OMTExpressionTestsHelper.CreateContext(new Point(0, 2.5))));
    }
}
