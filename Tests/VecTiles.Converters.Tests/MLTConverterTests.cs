using NetTopologySuite.Geometries;
using VecTiles.Common.Enums;
using VecTiles.Common.Primitives;
using VecTiles.Converters.MLT;
using VecTiles.DataSources.MbTiles;
using VecTiles.DataSources.PmTiles;
using VecTiles.TileDataSources;

using VecTiles.Tests;

namespace VecTiles.Converters.Tests;

/// <summary>
/// Tests for <see cref="MltTileConverter" /> with a synthetic MLT tile and with real
/// MLT data from the repository (MBTiles and PMTiles).
/// </summary>
public class MLTConverterTests
{
    readonly string _mbTilesPath = Path.Combine(RepoData.MapLibreTilesFiles, "zuerich.mbtiles");
    readonly string _pmTilesPath = Path.Combine(RepoData.MapLibreTilesFiles, "zuerich.pmtiles");

    /// <summary>
    /// Verifies that a hand encoded MLT tile with a point layer decodes to the expected
    /// geometry and attributes.
    /// </summary>
    [Fact]
    public void SyntheticPointLayerTest()
    {
        var data = BuildSyntheticTile(includeAttributes: false);
        var tile = new Tile(0, 0, 0);

        var vectorTile = new MltTileConverter().Convert(tile, tile, data).Result;

        Assert.NotNull(vectorTile);
        Assert.False(vectorTile.IsEmpty);
        var layer = Assert.Single(vectorTile.Layers);
        Assert.Equal("test", layer.Name);
        var feature = Assert.Single(layer.Features);
        var point = Assert.IsType<Point>(feature.Geometry);
        Assert.Equal(100, point.X, 5);
        Assert.Equal(200, point.Y, 5);
        Assert.Equal(0, feature.Attributes.Count);
    }

    /// <summary>
    /// Verifies that a hand encoded MLT tile with a nullable Int32 attribute column
    /// decodes the ZigZag encoded attribute value.
    /// </summary>
    [Fact]
    public void SyntheticAttributeTest()
    {
        var data = BuildSyntheticTile(includeAttributes: true);
        var tile = new Tile(0, 0, 0);

        var vectorTile = new MltTileConverter().Convert(tile, tile, data).Result;

        Assert.NotNull(vectorTile);
        var layer = Assert.Single(vectorTile.Layers);
        var feature = Assert.Single(layer.Features);
        Assert.Equal(2L, feature.Attributes["admin_level"]);
        Assert.Equal(100, Assert.IsType<Point>(feature.Geometry).X, 5);
    }

    /// <summary>
    /// Verifies that a Zurich MLT MBTiles tile converted by <see cref="MltTileConverter" />
    /// yields the expected layers, features and attributes.
    /// </summary>
    [Fact]
    public async Task MlTMbTilesConverterTest()
    {
        var dataSource = new MbTilesTileDataSource(_mbTilesPath, determineZoomLevelsFromTilesTable: true, determineTileRangeFromTilesTable: true);
        Assert.Equal("3.16.0", dataSource.Version);
        Assert.Equal(SourceType.Vector, dataSource.SourceType);

        var tileConverter = new MltTileConverter();
        var vectorDataSource = new VectorTileDataSource(dataSource, tileConverter);

        var vectorTile = await vectorDataSource.GetVectorTileAsync(new Tile(8580, 5738, 14));

        Assert.NotNull(vectorTile);
        Assert.False(vectorTile.IsEmpty);
        var layerNames = vectorTile.Layers.Select(l => l.Name).ToHashSet();
        Assert.Contains("building", layerNames);
        Assert.Contains("transportation", layerNames);
        Assert.Contains("water", layerNames);
        var buildingLayer = vectorTile.Layers.Single(l => l.Name == "building");
        Assert.NotEmpty(buildingLayer.Features);
        var renderedHeight = Convert.ToDouble(buildingLayer.Features[0].Attributes["render_height"]);
        Assert.True(renderedHeight > 0);
    }

    /// <summary>
    /// Verifies that a Zurich MLT PMTiles tile converted by <see cref="MltTileConverter" />
    /// yields the expected layers, features and attributes.
    /// </summary>
    [Fact]
    public async Task MlTPmTilesConverterTest()
    {
        using var dataSource = new PmTilesTileDataSource(_pmTilesPath);
        Assert.Equal(PmTilesTileType.MapLibreVector, dataSource.Header.TileType);

        var tileConverter = new MltTileConverter();
        var vectorDataSource = new VectorTileDataSource(dataSource, tileConverter);

        var vectorTile = await vectorDataSource.GetVectorTileAsync(new Tile(2146, 1433, 12));

        Assert.NotNull(vectorTile);
        Assert.False(vectorTile.IsEmpty);
        var aerodromeLabelLayer = vectorTile.Layers.SingleOrDefault(l => l.Name == "aerodrome_label");
        Assert.NotNull(aerodromeLabelLayer);
        var feature = Assert.Single(aerodromeLabelLayer.Features);
        Assert.Equal("Militärflugplatz Dübendorf", feature.Attributes["name"]);
        Assert.Equal("military", feature.Attributes["class"]);
        Assert.Equal("LSMD", feature.Attributes["icao"]);
        var point = Assert.IsType<Point>(feature.Geometry);
        Assert.Equal(1376d / 4096 * 512, point.X, 5);
        Assert.Equal(3989d / 4096 * 512, point.Y, 5);
    }

    /// <summary>
    /// Verifies that a MapLibre demotiles MLT tile whose geolines geometry uses a Morton
    /// encoded vertex dictionary decodes to lines with coordinates inside the plausible
    /// range around the tile. The dictionary is sorted, so its Morton deltas are plain
    /// cumulative values, not ZigZag encoded; a wrong ZigZag decoding produces huge
    /// wrapped coordinates (diagonal garbage lines).
    /// </summary>
    [Fact]
    public void MlTDemotilesMortonDictionaryTest()
    {
        var path = Path.Combine(RepoData.MapLibreTilesFiles, "demotiles-3-4-2.mlt");
        var data = File.ReadAllBytes(path);
        var tile = new Tile(4, 2, 3);

        var vectorTile = new MltTileConverter().Convert(tile, tile, data).Result;

        Assert.NotNull(vectorTile);
        var geolines = vectorTile.Layers.Single(l => l.Name == "geolines");
        Assert.NotEmpty(geolines.Features);
        foreach (var feature in geolines.Features)
        {
            var line = Assert.IsAssignableFrom<LineString>(feature.Geometry);
            Assert.NotEmpty(line.Coordinates);
            foreach (var coordinate in line.Coordinates)
            {
                Assert.InRange(coordinate.X, -2048, 2048);
                Assert.InRange(coordinate.Y, -2048, 2048);
            }

            if (feature.Attributes.Exists("id"))
            {
                // The id must be a ulong, like the MVT parser stores it, so the
                // symbol factories of the styles can unbox it without exception
                Assert.IsType<ulong>(feature.Attributes["id"]);
            }
        }

        // The tile must contain features with an id (e.g. named geolines or
        // countries), so the type check above is not vacuous
        var idCount = vectorTile.Layers.SelectMany(l => l.Features)
            .Count(f => f.Attributes.Exists("id"));
        Assert.True(idCount > 0, "No feature with an id attribute in the tile");
    }

    /// <summary>
    /// Builds a synthetic MLT tile with one layer "test", one point feature at (100, 200)
    /// and an optional nullable Int32 attribute column "admin_level" with the value 2
    /// (ZigZag encoded as 4).
    /// </summary>
    private static byte[] BuildSyntheticTile(bool includeAttributes)
    {
        // Layer body: name, extent, column count, column metas, column data
        var body = new List<byte>();
        body.AddRange(Varint("test".Length));
        body.AddRange("test"u8);
        body.AddRange(Varint(512));
        body.AddRange(Varint(includeAttributes ? 2 : 1));

        // Geometry column meta (type 4, no name)
        body.Add(4);

        // Optional Int32 attribute column meta (type 17, named)
        if (includeAttributes)
        {
            body.Add(17);
            body.AddRange(Varint("admin_level".Length));
            body.AddRange("admin_level"u8);
        }

        // Geometry column data: stream count, types stream, vertex stream
        body.AddRange(Varint(2));
        // Types stream (Data/None, None logical, VarInt physical): one feature, Point
        body.Add(0x10);
        body.Add(0x02);
        body.AddRange(Varint(1));
        body.AddRange(Varint(1));
        body.Add(0);
        // Vertex stream (Data/Vertex, ComponentwiseDelta, VarInt): deltas of (100, 200)
        body.Add(0x13);
        body.Add(0x42);
        body.AddRange(Varint(2));
        var vertexBytes = Varint(200).Concat(Varint(400)).ToArray();
        body.AddRange(Varint(vertexBytes.Length));
        body.AddRange(vertexBytes);

        // Optional Int32 attribute column data: Present stream, data stream
        if (includeAttributes)
        {
            // Present stream (no run counts): one present bit
            body.Add(0x00);
            body.Add(0x00);
            body.AddRange(Varint(1));
            body.AddRange(Varint(2));
            body.Add(0x00); // repeat run of control + 3 bytes
            body.Add(0x01); // bitmap byte: present
            // Data stream (None logical, VarInt physical): zigzag(2) = 4
            body.Add(0x10);
            body.Add(0x02);
            body.AddRange(Varint(1));
            body.AddRange(Varint(1));
            body.Add(4);
        }

        // Frame: varint size, layer tag, body
        var frame = new List<byte>();
        frame.AddRange(Varint(body.Count + 1));
        frame.Add(1);
        frame.AddRange(body);
        return [.. frame];
    }

    /// <summary>
    /// Encodes an unsigned variable width integer.
    /// </summary>
    private static byte[] Varint(long value)
    {
        var bytes = new List<byte>();
        while (value > 0x7f)
        {
            bytes.Add((byte)(0x80 | (value & 0x7f)));
            value >>= 7;
        }

        bytes.Add((byte)value);
        return [.. bytes];
    }
}
