using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using VecTiles.Common.Primitives;
using VecTiles.Styles.OpenMapTiles;
using Xunit.Abstractions;

namespace VecTiles.Renderers.Skia.Test;

/// <summary>
/// The road_shield layer of osm-liberty switches symbol-placement from point to
/// line at zoom 11 while keeping both rotation alignments on viewport. Per style
/// spec, line placement with a viewport rotation alignment behaves like point
/// placement, so the shield must not become a line symbol at those zooms.
/// </summary>
public class RoadShieldPlacementTests
{
    private const string _path = "Files";

    private readonly ITestOutputHelper _output;

    public RoadShieldPlacementTests(ITestOutputHelper output) => _output = output;

    private static (OMTSymbolFactory Factory, OMTLayerStyle Layer) CreateRoadShield()
    {
        var styleSource = $"file://{_path}/osm-liberty.json";
        var styleFile = OMTStyleFileLoader.Load(styleSource).Result;
        var layer = styleFile.Layers.First(l => l.Name == "road_shield");

        return (new OMTSymbolFactory(styleFile.Sprites!), layer);
    }

    private static IFeature CreateRoadFeature()
    {
        var geometry = new LineString(new[]
        {
            new Coordinate(10, 10),
            new Coordinate(200, 200),
            new Coordinate(400, 300),
        });

        return new Feature
        {
            Geometry = geometry,
            Attributes = new AttributesTable(new Dictionary<string, object>
            {
                ["id"] = 1UL,
                ["ref"] = "A1",
                ["name"] = "Teststrasse",
            }),
        };
    }

    [Theory]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(14)]
    public void RoadShieldStaysAPointSymbol(int zoom)
    {
        var (factory, layer) = CreateRoadShield();

        var symbol = factory.CreateSymbol(new Tile(0, 0, zoom), layer,
            new EvaluationContext(zoom), CreateRoadFeature());

        Assert.IsType<PointSymbol>(symbol);
        _output.WriteLine($"zoom {zoom}: {symbol?.GetType().Name}");
    }

    [Fact]
    public void RoadLabelsStayLineSymbols()
    {
        var styleSource = $"file://{_path}/osm-liberty.json";
        var styleFile = OMTStyleFileLoader.Load(styleSource).Result;
        var layer = styleFile.Layers.First(l => l.Name == "road_label");
        var factory = new OMTSymbolFactory(styleFile.Sprites!);

        var symbol = factory.CreateSymbol(new Tile(0, 0, 14), layer,
            new EvaluationContext(14), CreateRoadFeature());

        Assert.IsType<LineSymbol>(symbol);
    }
}
