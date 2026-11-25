using System.Text;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Converters.Mapsforge;
using VecTiles.DataSources.Mapsforge;
using VecTiles.Tests;

namespace VecTiles.Converters.Tests;

/// <summary>
/// The fixed caption anchor of ways: the map file stores an optional label
/// position (feature byte 0x10, VBE-S offsets relative to the first way node,
/// see readOptionalLabelPosition of the mapsforge reader) and the converter
/// always passes the bounding box center of the unclipped way as a fallback
/// anchor (see PolylineContainer.getCenterAbsolute). Both appear as tags
/// ("mf:label-x/y" and "mf:anchor-x/y") in normalized tile coordinates; the
/// renderer draws the caption only in the tile that contains the anchor point.
/// Verified on synthetic tiles and on the park paths "Platzpromenade" of
/// zuerich.map around tile 137289..137291 / 91792..91795 at zoom 18.
/// </summary>
public class MapsforgeLabelPositionTests
{
    private const int TileSize = 512;

    // ---------------------------------------------------------------------
    // Synthetic ways (build the tile envelope by hand, see
    // MapsforgeTileConverterTests for the envelope layout)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task LabelPositionIsRelativeToTheFirstWayNodeTest()
    {
        // Tile (8, 5) of zoom level 4. The way starts at the tile corner and
        // runs south east; the label position offsets (+50 km lon, 0 lat)
        // are relative to the first way node, not to the tile corner.
        const int baseZoom = 4;
        var tile = new Tile(8, 5, baseZoom);

        var wayData = new List<byte>();
        AddWayWithLabelPosition(wayData,
            [(0, 0), (0, 100_000)],
            latOffset: 0, lonOffset: 100_000, name: "Testweg");

        var envelope = BuildEnvelope(baseZoom, baseZoom, baseZoom, 8, 5, 1,
            [], ["highway=residential"],
            [(false, TileData([(0, 1)], 0, [], [.. wayData]))]);

        var vectorTile = await new MapsforgeTileConverter(new IdentityTagsConverter("mapsforge")).Convert(tile, tile, envelope);

        var way = SingleWay(vectorTile!);
        Assert.Equal(
            TileXOfLongitudeDelta(100_000, tile),
            TileSpaceValue(way, "mf:label-x"), 3);
        Assert.Equal(
            TileYOfLatitudeDelta(0, tile),
            TileSpaceValue(way, "mf:label-y"), 3);
    }

    [Fact]
    public async Task LabelPositionOrderIsLatitudeThenLongitudeTest()
    {
        // Guards against a lat/lon offset swap: the first VBE-S value after
        // the reference/feature flags is the latitude offset (the second the
        // longitude offset, see readOptionalLabelPosition of the mapsforge
        // reader). The offsets move the label into the south east of the way
        const int baseZoom = 4;
        var tile = new Tile(8, 5, baseZoom);

        var wayData = new List<byte>();
        AddWayWithLabelPosition(wayData,
            [(0, 0), (0, 100_000)],
            latOffset: -50_000, lonOffset: 50_000, name: "Testweg");

        var envelope = BuildEnvelope(baseZoom, baseZoom, baseZoom, 8, 5, 1,
            [], ["highway=residential"],
            [(false, TileData([(0, 1)], 0, [], [.. wayData]))]);

        var vectorTile = await new MapsforgeTileConverter(new IdentityTagsConverter("mapsforge")).Convert(tile, tile, envelope);

        var way = SingleWay(vectorTile!);
        Assert.Equal(
            TileYOfLatitudeDelta(-50_000, tile),
            TileSpaceValue(way, "mf:label-y"), 3);
        Assert.Equal(
            TileXOfLongitudeDelta(50_000, tile),
            TileSpaceValue(way, "mf:label-x"), 3);
    }

    [Fact]
    public async Task WayAnchorIsTheBoundingBoxCenterTest()
    {
        // An open way from the tile corner to (+0.1° lon, -0.1° lat): the
        // anchor is the center of the way bounding box, regardless of the
        // geometry
        const int baseZoom = 4;
        var tile = new Tile(8, 5, baseZoom);

        var wayData = new List<byte>();
        AddWayWithLabelPosition(wayData,
            [(0, 0), (-50_000, 50_000), (-100_000, 100_000)],
            latOffset: null, lonOffset: null, name: "Testweg");

        var envelope = BuildEnvelope(baseZoom, baseZoom, baseZoom, 8, 5, 1,
            [], ["highway=residential"],
            [(false, TileData([(0, 1)], 0, [], [.. wayData]))]);

        var vectorTile = await new MapsforgeTileConverter(new IdentityTagsConverter("mapsforge")).Convert(tile, tile, envelope);

        var way = SingleWay(vectorTile!);
        Assert.False(way.Attributes!.Exists("mf:label-x"), "A way without a label position must not carry label tags");
        Assert.Equal(
            TileXOfLongitudeDelta(50_000, tile),
            TileSpaceValue(way, "mf:anchor-x"), 3);
        Assert.Equal(
            TileYOfLatitudeDelta(-50_000, tile),
            TileSpaceValue(way, "mf:anchor-y"), 3);
    }

    // ---------------------------------------------------------------------
    // Real map: the caption anchor of the Platzpromenade paths is a fixed
    // geographic point that spans the neighbouring tiles
    // ---------------------------------------------------------------------

    private const string ParkName = "Platzpromenade";

    private static readonly Tile[] _parkTiles =
        Enumerable.Range(137289, 3)
            .SelectMany(x => Enumerable.Range(91792, 4).Select(y => new Tile(x, y, 18)))
            .ToArray();

    [Fact]
    public async Task PlatzpromenadeAnchorIsFixedAcrossTheNeighbouringTilesTest()
    {
        var copies = new List<(Tile Tile, IFeature Copy, double X, double Y)>();
        foreach (var tile in _parkTiles)
        {
            foreach (var copy in await CopiesNamed(tile, ParkName))
            {
                Assert.True(copy.Attributes!.Exists("mf:anchor-x") && copy.Attributes.Exists("mf:anchor-y"),
                    $"Copy of '{ParkName}' in tile {tile.X}/{tile.Y} has no anchor tags");

                var (worldX, worldY) = WorldOf(tile,
                    TileSpaceValue(copy, "mf:anchor-x"), TileSpaceValue(copy, "mf:anchor-y"));
                copies.Add((tile, copy, worldX, worldY));
            }
        }

        Assert.NotEmpty(copies);

        // The plaza paths are several separately stored ways, each with its own
        // fixed anchor: group the copies by their anchor world point (the
        // anchor of a way is computed identically in every tile) and check that
        // each way has the anchor of exactly one of its tiles in the half open
        // tile space - exactly this tile draws the caption, once and at a fixed
        // geographic position, independently of the tile set
        var ways = copies.GroupBy(s => (Math.Round(s.X, 4), Math.Round(s.Y, 4))).ToList();
        Assert.All(ways, way => Assert.Equal(1, way.Count(s =>
            TileSpaceValue(s.Copy, "mf:anchor-x") is >= 0.0 and < TileSize &&
            TileSpaceValue(s.Copy, "mf:anchor-y") is >= 0.0 and < TileSize)));
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private static IFeature SingleWay(VectorTile vectorTile)
    {
        var features = vectorTile.Layers.SelectMany(l => l.Features)
            .Where(f => f.Geometry is LineString or Polygon).ToList();
        return Assert.Single(features);
    }

    private static double TileSpaceValue(IFeature feature, string key) =>
        Convert.ToDouble(feature.Attributes![key], System.Globalization.CultureInfo.InvariantCulture);

    private static (double X, double Y) WorldOf(Tile tile, double x, double y) =>
        (tile.Left + (tile.Right - tile.Left) * x / TileSize,
            tile.Top - (tile.Top - tile.Bottom) * y / TileSize);

    private static async Task<List<IFeature>> CopiesNamed(Tile tile, string name)
    {
        using var dataSource = new MapsforgeTileDataSource(
            [Path.Combine(RepoData.MapsforgeFiles, "zuerich.map")], 0, 21);

        var provided = tile;
        while (await dataSource.GetTileAsync(provided) is not byte[] envelope && provided.Zoom > 0)
            provided = provided.Parent!;
        var data = await dataSource.GetTileAsync(provided)
            ?? throw new InvalidOperationException("No tile data in the map file");

        var vectorTile = await new MapsforgeTileConverter(new IdentityTagsConverter("mapsforge")).Convert(tile, provided, data)
            ?? throw new InvalidOperationException("The converter returned no vector tile");

        return vectorTile.Layers
            .SelectMany(l => l.Features)
            .Where(f => f.Attributes is not null &&
                        f.Attributes.Exists("name") &&
                        (f.Attributes["name"] as string) == name)
            .ToList();
    }

    private static double TileXOfLongitudeDelta(long lonDelta, Tile tile)
    {
        var cornerLongitude = tile.X / Math.Pow(2, tile.Zoom) * 360.0 - 180.0;

        return (cornerLongitude + lonDelta / 1_000_000.0 + 180.0) / 360.0 * 512.0 * Math.Pow(2, tile.Zoom)
            - tile.X * 512.0;
    }

    private static double TileYOfLatitudeDelta(long latDelta, Tile tile)
    {
        var cornerLatitude = Math.Atan(Math.Sinh(Math.PI * (1 - 2.0 * tile.Y / Math.Pow(2, tile.Zoom))));
        var latitudeRadians = cornerLatitude + latDelta / 1_000_000.0 * Math.PI / 180.0;

        return (1.0 - Math.Log(Math.Tan(Math.PI / 4.0 + latitudeRadians / 2.0)) / Math.PI) / 2.0
            * 512.0 * Math.Pow(2, tile.Zoom) - tile.Y * 512.0;
    }

    /// <summary>
    /// Add a named way with an optional label position to the given tile data
    /// bytes: single delta encoded nodes, the label position flag (0x10) and
    /// the name flag (0x80) in the feature byte, VBE-S offsets relative to the
    /// first way node.
    /// </summary>
    private static void AddWayWithLabelPosition(List<byte> data, List<(long Lat, long Lon)> nodes,
        long? latOffset, long? lonOffset, string name)
    {
        var properties = new List<byte>();
        properties.AddRange([0x00, 0x00]); // way tile bitmask
        properties.Add(0x01); // layer 0, tag count 1
        properties.AddRange(WriteVbeu(0));

        byte featureByte = 0x80; // name follows
        if (latOffset.HasValue && lonOffset.HasValue)
        {
            featureByte |= 0x10;
        }

        properties.Add(featureByte);
        AddString(properties, name);
        if (latOffset.HasValue && lonOffset.HasValue)
        {
            properties.AddRange(WriteVbeS(latOffset.Value));
            properties.AddRange(WriteVbeS(lonOffset.Value));
        }

        var blocks = new List<byte>();
        blocks.AddRange(WriteVbeu(1)); // one way data block with one coordinate block
        blocks.AddRange(WriteVbeu(nodes.Count));
        blocks.AddRange(WriteVbeS(nodes[0].Lat));
        blocks.AddRange(WriteVbeS(nodes[0].Lon));
        for (var i = 1; i < nodes.Count; i++)
        {
            blocks.AddRange(WriteVbeS(nodes[i].Lat - nodes[i - 1].Lat));
            blocks.AddRange(WriteVbeS(nodes[i].Lon - nodes[i - 1].Lon));
        }

        data.AddRange(WriteVbeu(properties.Count + blocks.Count));
        data.AddRange(properties);
        data.AddRange(blocks);
    }

    private static byte[] TileData((int Poi, int Way)[] zoomTableRows,
        int firstWayOffset, byte[] poiData, byte[] wayData)
    {
        var tileData = new List<byte>();
        foreach (var (poi, way) in zoomTableRows)
        {
            tileData.AddRange(WriteVbeu(poi));
            tileData.AddRange(WriteVbeu(way));
        }

        tileData.AddRange(WriteVbeu(firstWayOffset));
        tileData.AddRange(poiData);
        tileData.AddRange(wayData);

        return [.. tileData];
    }

    private static byte[] BuildEnvelope(int baseZoom, int minZoom, int maxZoom, int firstTileX, int firstTileY,
        int columns, string[] poiTags, string[] wayTags, List<(bool Water, byte[] Data)> tiles)
    {
        var envelope = new List<byte>();
        envelope.Add(0x00); // no meta flags: the envelope carries tile data
        envelope.AddRange(WriteVbeu(baseZoom));
        envelope.AddRange(WriteVbeu(minZoom));
        envelope.AddRange(WriteVbeu(maxZoom));
        envelope.AddRange(WriteVbeu(firstTileX));
        envelope.AddRange(WriteVbeu(firstTileY));
        envelope.AddRange(WriteVbeu(columns));
        envelope.AddRange(WriteVbeu(tiles.Count));

        AddTable(envelope, poiTags);
        AddTable(envelope, wayTags);

        foreach (var (isWater, data) in tiles)
        {
            envelope.Add(isWater ? (byte)0x01 : (byte)0x00);
            envelope.AddRange(WriteVbeu(data.Length));
            envelope.AddRange(data);
        }

        return [.. envelope];

        static void AddTable(List<byte> envelope, string[] strings)
        {
            envelope.AddRange(WriteVbeu(strings.Length));
            foreach (var value in strings)
            {
                var bytes = Encoding.UTF8.GetBytes(value);
                envelope.AddRange(WriteVbeu(bytes.Length));
                envelope.AddRange(bytes);
            }
        }
    }

    private static byte[] WriteVbeu(long value)
    {
        var bytes = new List<byte>();
        do
        {
            var currentByte = (byte)(value & 0x7f);
            value >>= 7;
            if (value != 0)
                currentByte |= 0x80;

            bytes.Add(currentByte);
        }
        while (value != 0);

        return [.. bytes];
    }

    private static byte[] WriteVbeS(long value)
    {
        var negative = value < 0;
        var magnitude = negative ? -value : value;
        var bytes = new List<byte>();
        while (magnitude > 0x3f)
        {
            bytes.Add((byte)((magnitude & 0x7f) | 0x80));
            magnitude >>= 7;
        }

        bytes.Add((byte)(negative ? magnitude | 0x40 : magnitude));

        return [.. bytes];
    }

    private static void AddString(List<byte> data, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        data.AddRange(WriteVbeu(bytes.Length));
        data.AddRange(bytes);
    }
}
