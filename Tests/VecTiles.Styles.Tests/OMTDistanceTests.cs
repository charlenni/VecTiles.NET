using NetTopologySuite.Geometries;
using Newtonsoft.Json.Linq;
using VecTiles.Styles.OpenMapTiles.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures
/// distance.a:
/// ["distance", { "type": "LineString", "coordinates": [[0, 0], [0, 5], [5, 5], [5, 0]] }]
/// distance.b: differs in the y coordinate of the second vertex.
/// </summary>
public class OMTDistanceTests
{
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = OMTExpressionFixture.Parse("distance", "a");
        var expressionA2 = OMTExpressionFixture.Parse("distance", "a");

        Assert.NotNull(expressionA1);
        Assert.NotNull(expressionA2);
        Assert.Equal(expressionA1, expressionA2);
    }

    [Fact]
    public void FixtureIsUnequalToOtherFixtureTest()
    {
        var expressionA = OMTExpressionFixture.Parse("distance", "a");
        var expressionB = OMTExpressionFixture.Parse("distance", "b");

        Assert.NotNull(expressionA);
        Assert.NotNull(expressionB);
        Assert.NotEqual(expressionA, expressionB);
    }

    [Fact]
    public void FeatureOnTheGeometryTest()
    {
        var expression = OMTExpressionFixture.Parse("distance", "a");

        Assert.NotNull(expression);

        // The feature point is the first vertex of the fixture geometry
        var distance = expression!.Evaluate(OMTExpressionTestsHelper.CreateContext());

        Assert.NotNull(distance);
        Assert.InRange((double)distance!, 0d, 1d);
    }

    [Fact]
    public void FeatureInsideTheLineRingTest()
    {
        var expression = OMTExpressionFixture.Parse("distance", "a");

        Assert.NotNull(expression);

        // The distance is measured to the nearest segment, here 2.5 degrees
        // north of the segment from (0, 0) to (0, 5)
        var distance = expression!.Evaluate(OMTExpressionTestsHelper.CreateContext(new Point(2.5, 2.5)));

        Assert.NotNull(distance);
        Assert.InRange((double)distance!, 277000d, 278500d);
    }
}
