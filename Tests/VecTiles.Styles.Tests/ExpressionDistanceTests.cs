using NetTopologySuite.Geometries;
using System.Text.Json.Nodes;
using VecTiles.Styles.MapLibre.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures
/// distance.a:
/// ["distance", { "type": "LineString", "coordinates": [[0, 0], [0, 5], [5, 5], [5, 0]] }]
/// distance.b: differs in the y coordinate of the second vertex.
/// </summary>
public class ExpressionDistanceTests
{
    /// <summary>
    /// Verifies that fixture a is structurally equal to a second parse of the same fixture.
    /// </summary>
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = ExpressionFixture.Parse("distance", "a");
        var expressionA2 = ExpressionFixture.Parse("distance", "a");

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
        var expressionA = ExpressionFixture.Parse("distance", "a");
        var expressionB = ExpressionFixture.Parse("distance", "b");

        Assert.NotNull(expressionA);
        Assert.NotNull(expressionB);
        Assert.NotEqual(expressionA, expressionB);
    }

    /// <summary>
    /// Verifies that the distance is (almost) zero when the feature point lies on the fixture geometry.
    /// </summary>
    [Fact]
    public void FeatureOnTheGeometryTest()
    {
        var expression = ExpressionFixture.Parse("distance", "a");

        Assert.NotNull(expression);

        // The feature point is the first vertex of the fixture geometry
        var distance = expression!.Evaluate(ExpressionTestsHelper.CreateContext());

        Assert.NotNull(distance);
        Assert.InRange((double)distance!, 0d, 1d);
    }

    /// <summary>
    /// Verifies that the distance for a point inside the line ring is measured to the nearest segment.
    /// </summary>
    [Fact]
    public void FeatureInsideTheLineRingTest()
    {
        var expression = ExpressionFixture.Parse("distance", "a");

        Assert.NotNull(expression);

        // The distance is measured to the nearest segment, here 2.5 degrees
        // north of the segment from (0, 0) to (0, 5)
        var distance = expression!.Evaluate(ExpressionTestsHelper.CreateContext(new Point(2.5, 2.5)));

        Assert.NotNull(distance);
        Assert.InRange((double)distance!, 277000d, 278500d);
    }
}
