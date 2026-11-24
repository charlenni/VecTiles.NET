using VecTiles.Common.Primitives;
using VecTiles.Converters.OpenMapTiles;
using VecTiles.DataSources.MbTiles;
using VecTiles.TileDataSources;
using Xunit.Abstractions;

using VecTiles.Tests;

namespace VecTiles.Converters.Tests;

/// <summary>
/// Dumps poi and waterway features of tile 17/68642/45902 (XYZ) for the
/// Schanzengraben overlap analysis.
/// </summary>
public class SchanzengrabenDumpTests
{
    readonly string _path = Path.Combine(RepoData.DataFiles, "zurich.mbtiles");
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Initializes the test with the xUnit output helper.
    /// </summary>
    /// <param name="output">The xUnit output helper for logging dumped features.</param>
    public SchanzengrabenDumpTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Writes the poi and waterway features of the tile and its neighbors to the test output.
    /// </summary>
    [Fact]
    public async Task DumpPoiAndWaterwayFeatures()
    {
        var dataSource = new MbTilesTileDataSource(_path, determineZoomLevelsFromTilesTable: true, determineTileRangeFromTilesTable: true);
        var tileConverter = new OMTTileConverter();
        var vectorDataSource = new VectorTileDataSource(dataSource, tileConverter);

        var tile = new Tile(68642, 45902, 17);

        var vectorTile = await vectorDataSource.GetVectorTileAsync(tile);

        Assert.NotNull(vectorTile);

        foreach (var layer in vectorTile.Layers)
        {
            if (layer.Name is not ("poi" or "waterway"))
            {
                continue;
            }

            _output.WriteLine($"LAYER '{layer.Name}': {layer.Features.Count} features");

            foreach (var feature in layer.Features)
            {
                var attrs = feature.Attributes;
                var names = attrs.GetNames();
                var dict = new Dictionary<string, string>();
                for (var i = 0; i < names.Length; i++)
                {
                    dict[names[i]] = attrs.GetValues()[i]?.ToString() ?? "";
                }

                var relevant = string.Join(", ", dict
                    .Where(kv => kv.Key is "id" or "class" or "subclass" or "name" or "rank")
                    .Select(kv => $"{kv.Key}={kv.Value}"));

                _output.WriteLine($"  geom={feature.Geometry.GeometryType} {relevant} pos=({feature.Geometry.EnvelopeInternal})");
            }
        }

        // Also dump the neighboring tiles for the canoe_polo feature
        foreach (var neighbor in new[] { (68641, 45902), (68643, 45902), (68642, 45901), (68642, 45903) })
        {
            vectorTile = await vectorDataSource.GetVectorTileAsync(new Tile(neighbor.Item1, neighbor.Item2, 17));
            if (vectorTile == null)
            {
                _output.WriteLine($"TILE {neighbor.Item1}/{neighbor.Item2}: null");
                continue;
            }

            var poiLayer = vectorTile.Layers.FirstOrDefault(l => l.Name == "poi");
            if (poiLayer == null)
            {
                _output.WriteLine($"TILE {neighbor.Item1}/{neighbor.Item2}: no poi layer");
                continue;
            }

            foreach (var feature in poiLayer.Features)
            {
                var subclass = feature.Attributes.GetOptionalValue("subclass")?.ToString();
                if (subclass == "canoe_polo")
                {
                    _output.WriteLine($"TILE {neighbor.Item1}/{neighbor.Item2}: canoe_polo id={feature.Attributes.GetOptionalValue("id")} " +
                        $"env={feature.Geometry.EnvelopeInternal}");
                }
            }
        }
    }
}
