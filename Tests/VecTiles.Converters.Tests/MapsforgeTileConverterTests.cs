using System.Text;
using NetTopologySuite.Geometries;
using VecTiles.Common.Primitives;
using VecTiles.Converters.Mapsforge;

namespace VecTiles.Converters.Tests;

/// <summary>
/// Tests for <see cref="MapsforgeTileConverter"/>: the decoding of tile envelopes (water flag,
/// zoom table, POIs and ways) and the transformation of the microdegree coordinates to the
/// normalized 512x512 tile space.
/// </summary>
public class MapsforgeTileConverterTests
{
    /// <summary>
    /// A completely water tile envelope decodes to a water layer with the full tile polygon.
    /// </summary>
    [Fact]
    public async Task WaterEnvelopeTest()
    {
        var vectorTile = await new MapsforgeTileConverter().Convert(Tile(8, 5, 4), Tile(8, 5, 4), [0x01]);

        Assert.NotNull(vectorTile);
        var layer = Assert.Single(vectorTile.Layers);
        Assert.Equal("water", layer.Name);
        var feature = Assert.Single(layer.Features);
        Assert.Equal("lake", feature.Attributes["class"]);
        // The polygon covers the full tile space
        Assert.Equal(0.0, feature.Geometry.EnvelopeInternal.MinX);
        Assert.Equal(0.0, feature.Geometry.EnvelopeInternal.MinY);
        Assert.Equal(512.0, feature.Geometry.EnvelopeInternal.MaxX);
        Assert.Equal(512.0, feature.Geometry.EnvelopeInternal.MaxY);
    }

    /// <summary>
    /// A POI and a polygon way of a synthetic tile decode to the coordinates of the tile space:
    /// the tile corner maps to (0, 0) and the offsets follow the mercator projection.
    /// </summary>
    [Fact]
    public async Task SyntheticTileCoordinatesTest()
    {
        // Tile (8, 5) of zoom level 4, the POIs and ways are relative to its top-left corner
        const int baseZoom = 4;
        var tile = Tile(8, 5, baseZoom);

        // One POI at the tile corner and one at (+0.1° lon, +0.1° lat south) from it: latitude
        // deltas are geographic, a point inside the tile lies south of the top-left corner
        var poiData = new List<byte>();
        AddPoint(poiData, 0, 0, [0]);
        AddPoint(poiData, -1_000_000, +100_000, [0]);

        // A closed way around the tile corner: a square of 0.1° per side with a building tag
        var wayNodes = new List<(long Lat, long Lon)>
        {
            (0, 0),
            (-100_000, 0),
            (-100_000, 100_000),
            (0, 100_000),
            (0, 0),
        };
        var wayData = new List<byte>();
        AddWay(wayData, wayNodes, [0]);

        var envelope = BuildEnvelope(baseZoom, baseZoom, baseZoom, 8, 5, 1,
            ["amenity=restaurant"], ["building=yes"],
            [(false, TileData([(2, 1)], poiData.Count, [.. poiData], [.. wayData]))]);

        var vectorTile = await new MapsforgeTileConverter().Convert(tile, tile, envelope);

        Assert.NotNull(vectorTile);
        Assert.Equal(2, vectorTile.Layers.Count);

        // The POIs are points in the poi layer
        var poiLayer = vectorTile.Layers.Single(l => l.Name == "poi");
        Assert.Equal(2, poiLayer.Features.Count);
        Assert.Equal(0.0, ((Point)poiLayer.Features[0].Geometry).X, 3);
        Assert.Equal(0.0, ((Point)poiLayer.Features[0].Geometry).Y, 3);
        Assert.Equal(TileXOfLongitudeDelta(100_000, tile),
            ((Point)poiLayer.Features[1].Geometry).X, 3);
        Assert.Equal(TileYOfLatitudeDelta(-1_000_000, tile),
            ((Point)poiLayer.Features[1].Geometry).Y, 3);

        // The way is a closed ring and carries an area tag: a building in the building layer
        var buildingLayer = vectorTile.Layers.Single(l => l.Name == "building");
        var building = Assert.Single(buildingLayer.Features);
        var polygon = Assert.IsType<Polygon>(building.Geometry);
        Assert.Equal(0.0, polygon.Shell.Coordinates[0].X, 3);
        Assert.Equal(0.0, polygon.Shell.Coordinates[0].Y, 3);
        Assert.Equal(0.0, polygon.Shell.Coordinates[1].X, 3);
        Assert.Equal(TileYOfLatitudeDelta(-100_000, tile), polygon.Shell.Coordinates[1].Y, 3);
        Assert.Equal(TileXOfLongitudeDelta(100_000, tile), polygon.Shell.Coordinates[2].X, 3);
        Assert.Equal(TileYOfLatitudeDelta(-100_000, tile), polygon.Shell.Coordinates[2].Y, 3);
    }

    /// <summary>
    /// The zoom table row of the requested zoom level selects the POIs and ways to decode.
    /// </summary>
    [Fact]
    public async Task ZoomTableRowSelectsFeaturesTest()
    {
        const int minZoom = 4;
        const int maxZoom = 6;

        // Two POIs and two open waterway lines (lines stay in the waterway layer, closed ways
        // with a water value would become water areas), south-east of the tile corner; the
        // zoom table rows are cumulative: row 0 selects none, row 1 selects the first of each,
        // row 2 selects all
        var poiData = new List<byte>();
        AddPoint(poiData, -10_000, 10_000, [0]);
        AddPoint(poiData, -20_000, 20_000, [0]);
        var wayData = new List<byte>();
        AddWay(wayData,
        [
            (0, 0),
            (-10_000, 0),
            (0, 10_000),
        ], [0]);
        AddWay(wayData,
        [
            (0, 0),
            (-20_000, 0),
            (0, 20_000),
        ], [0]);

        // The stored zoom table rows are per zoom level; their cumulative sums select 0, then
        // 1, then 2 of the POIs and ways; the base zoom is the middle zoom level: the tile grid
        // coordinates and the microdegree deltas refer to that zoom level
        var envelope = BuildEnvelope(5, minZoom, maxZoom, 8, 5, 1, ["amenity=restaurant"], ["waterway=stream"],
            [(false, TileData([(0, 0), (1, 1), (1, 1)], poiData.Count, [.. poiData], [.. wayData]))]);

        // The requested zoom level 5 maps to zoom table row 1: one POI, one way
        var tile = await new MapsforgeTileConverter().Convert(Tile(8, 5, 5), Tile(8, 5, 5), envelope);
        Assert.NotNull(tile);
        Assert.Equal(1, tile.Layers.Single(l => l.Name == "poi").Features.Count);
        Assert.Equal(1, tile.Layers.Single(l => l.Name == "waterway").Features.Count);

        // The requested zoom level 6 maps to zoom table row 2: both POIs, both ways; the
        // requested tile (16, 10, 6) covers the top-left quadrant of the base tile (8, 5, 5),
        // whose data the data source provides
        var tile6 = await new MapsforgeTileConverter().Convert(Tile(16, 10, 6), Tile(8, 5, 5), envelope);
        Assert.NotNull(tile6);
        Assert.Equal(2, tile6.Layers.Single(l => l.Name == "poi").Features.Count);
        Assert.Equal(2, tile6.Layers.Single(l => l.Name == "waterway").Features.Count);
    }

    /// <summary>
    /// A way crossing the shared border of two vertically neighbouring overzoom tiles must be
    /// present in both tiles, with its run end point at its original coordinate outside of the
    /// tile: the line then reaches the tile border everywhere and the neighbouring tile
    /// continues it seamlessly without a gap at the clipping point.
    /// </summary>
    [Fact]
    public async Task WayCrossingTileBorderContinuesTest()
    {
        const int baseZoom = 4;

        // A vertical way from +0.1 latitude delta (north of the base tile (8, 5) top border)
        // down to -0.8 (south of the shared border of the overzoom tiles (16, 10, 5) and
        // (16, 11, 5), which the base tile covers): it crosses the shared border of the two
        // neighbouring overzoom tiles
        var wayData = new List<byte>();
        AddWay(wayData, [(100_000, 150_000), (-8_000_000, 150_000)], [0]);
        var envelope = BuildEnvelope(baseZoom, baseZoom, baseZoom, 8, 5, 1, [], ["highway=residential"],
            [(false, TileData([(0, 1)], 0, [], [.. wayData]))]);

        var north = await new MapsforgeTileConverter().Convert(Tile(16, 10, 5), Tile(8, 5, baseZoom), envelope);
        var south = await new MapsforgeTileConverter().Convert(Tile(16, 11, 5), Tile(8, 5, baseZoom), envelope);

        Assert.NotNull(north);
        Assert.NotNull(south);

        var northLine = north.Layers.SelectMany(l => l.Features)
            .Single(f => f.Geometry is LineString or MultiLineString);
        var southLine = south.Layers.SelectMany(l => l.Features)
            .Single(f => f.Geometry is LineString or MultiLineString);

        // The way reaches the shared border from both tiles and extends beyond it: the run
        // end points keep their original coordinates outside of the clip rectangle
        Assert.True(northLine.Geometry.EnvelopeInternal.MaxY > 512,
            $"The way does not reach the shared border in the northern tile: {northLine.Geometry.EnvelopeInternal}");
        Assert.True(southLine.Geometry.EnvelopeInternal.MinY < 0,
            $"The way does not reach the shared border in the southern tile: {southLine.Geometry.EnvelopeInternal}");

        // The overhanging ends must carry valid coordinates
        foreach (var geometry in new[] { northLine.Geometry, southLine.Geometry })
        {
            foreach (var coordinate in geometry.Coordinates)
            {
                Assert.True(double.IsFinite(coordinate.X) && double.IsFinite(coordinate.Y),
                    $"Non-finite coordinate of a crossing way: {coordinate}");
            }
        }
    }

    /// <summary>
    /// A way crossing the corner of an overzoom tile (two adjacent clip rectangle edges, e.g.
    /// from north of the tile to west of it) must produce a run in that tile even though no
    /// vertex lies inside it: such a segment spans only one edge of each axis of the clip
    /// rectangle, a side-spanning pre-filter would reject it.
    /// </summary>
    [Fact]
    public async Task WayCrossingTileCornerContinuesTest()
    {
        const int baseZoom = 4;

        // A diagonal way passing the north-west corner of the overzoom tile (16, 10, 5), which
        // the base tile (8, 5) covers: the first node lies north of the tile top border, the
        // second west of the tile left border; both keep the same distance from the corner, so
        // the way crosses the clip rectangle through its top and left edge near the corner
        // without any vertex inside the rectangle.
        var wayData = new List<byte>();
        AddWay(wayData, [(790_000, 1_318_400), (-940_000, -1_230_500)], [0]);
        var envelope = BuildEnvelope(baseZoom, baseZoom, baseZoom, 8, 5, 1, [], ["highway=residential"],
            [(false, TileData([(0, 1)], 0, [], [.. wayData]))]);

        var tile = await new MapsforgeTileConverter().Convert(Tile(16, 10, 5), Tile(8, 5, baseZoom), envelope);

        Assert.NotNull(tile);
        var line = tile.Layers.SelectMany(l => l.Features)
            .Single(f => f.Geometry is LineString or MultiLineString);

        var envelopeInternal = line.Geometry.EnvelopeInternal;
        // The run crosses the tile corner region (with a margin against the exact corner so it
        // also survives setup coordinate rounding)
        Assert.True(envelopeInternal.Intersects(new Envelope(0, 512, 0, 512)),
            $"The way does not pass the tile corner: {envelopeInternal}");

        // The run points carry valid coordinates
        foreach (var coordinate in line.Geometry.Coordinates)
        {
            Assert.True(double.IsFinite(coordinate.X) && double.IsFinite(coordinate.Y),
                $"Non-finite coordinate of a corner-crossing way: {coordinate}");
        }
    }

    /// <summary>
    /// An injected ITagsConverter decides the source layer and the tags of the features.
    /// </summary>
    [Fact]
    public async Task TagsConverterInjectionTest()
    {
        var poiData = new List<byte>();
        AddPoint(poiData, -10_000, 10_000, [0]);
        var envelope = BuildEnvelope(4, 4, 4, 8, 5, 1, ["amenity=restaurant"], [],
            [(false, TileData([(1, 0)], poiData.Count, [.. poiData], []))]);

        var tile = await new MapsforgeTileConverter(new OneLayerTagsConverter("everything"))
            .Convert(Tile(8, 5, 4), Tile(8, 5, 4), envelope);

        Assert.NotNull(tile);
        var layer = Assert.Single(tile.Layers);
        Assert.Equal("everything", layer.Name);
        var feature = Assert.Single(layer.Features);
        // The tags converter kept the raw tags
        Assert.Equal("restaurant", feature.Attributes["amenity"]);
    }

    /// <summary>
    /// The tag to source layer mappings follow the OpenMapTiles mapping.yaml definitions: a
    /// peak point, a park polygon, a platform line and a borderless boundary line map to the
    /// corresponding source layers of the schema.
    /// </summary>
    [Fact]
    public async Task OpenMapTilesMappingTest()
    {
        // One POI per kind: peak point (mountain_peak), park polygon (landcover), park
        // boundary polygon (park layer), platform line, administrative boundary line
        var poiData = new List<byte>();
        AddPoint(poiData, -10_000, 10_000, [0]); // tagged with natural=peak
        var wayData = new List<byte>();
        AddWay(wayData,
        [
            (0, 0),
            (-100_000, 0),
            (-100_000, 100_000),
            (0, 100_000),
            (0, 0),
        ], [0]); // tagged with leisure=park
        AddWay(wayData,
        [
            (0, 0),
            (-100_000, 0),
            (-100_000, 100_000),
            (0, 100_000),
            (0, 0),
        ], [1]); // tagged with boundary=national_park
        AddWay(wayData,
        [
            (0, 0),
            (-100_000, 0),
        ], [2]); // tagged with railway=platform
        AddWay(wayData,
        [
            (0, 100_000),
            (-100_000, 100_000),
        ], [3]); // tagged with boundary=administrative (without admin level)

        var envelope = BuildEnvelope(6, 6, 6, 8, 5, 1,
            ["natural=peak"],
            ["leisure=park", "boundary=national_park", "railway=platform", "boundary=administrative"],
            [(false, TileData([(1, 4)], poiData.Count, [.. poiData], [.. wayData]))]);

        var tile = await new MapsforgeTileConverter().Convert(Tile(8, 5, 6), Tile(8, 5, 6), envelope);

        Assert.NotNull(tile);
        Assert.Equal("mountain_peak", Assert.Single(tile.Layers, l => l.Name == "mountain_peak").Name);
        Assert.Single(tile.Layers.Single(l => l.Name == "mountain_peak").Features);

        // A park polygon is a land cover area of class 'grass' (leisure park) per the class
        // field table of the landcover layer
        var landcover = tile.Layers.Single(l => l.Name == "landcover");
        var parkFeature = Assert.Single(landcover.Features);
        Assert.Equal("grass", parkFeature.Attributes["class"]);
        Assert.Equal("park", parkFeature.Attributes["subclass"]);

        // A national park polygon is a park of its raw boundary value
        var park = tile.Layers.Single(l => l.Name == "park");
        Assert.Equal("national_park", Assert.Single(park.Features).Attributes["class"]);

        // A platform line belongs to class 'path' of the transportation layer
        var platform = tile.Layers.Single(l => l.Name == "transportation")
            .Features.Single(f => (string)f.Attributes["subclass"] == "platform");
        Assert.Equal("path", platform.Attributes["class"]);

        // An administrative boundary without admin level still maps to the boundary layer
        var boundary = tile.Layers.Single(l => l.Name == "boundary");
        Assert.Single(boundary.Features);
        Assert.False(boundary.Features[0].Attributes.Exists("admin_level"));
    }

    /// <summary>
    /// A named street of the real zuerich.map decodes to a road in the transportation layer and
    /// to a street name in the transportation_name layer.
    /// </summary>
    [Fact]
    public async Task ZurichMapTransportationTest()
    {
        var vectorTile = await ConvertZurichTile(8.5417, 47.3765, 15);

        var transportation = vectorTile.Layers.FirstOrDefault(l => l.Name == "transportation");
        Assert.NotNull(transportation);
        Assert.NotEmpty(transportation.Features);

        var streetNames = vectorTile.Layers.FirstOrDefault(l => l.Name == "transportation_name");
        Assert.NotNull(streetNames);

        // The named roads carry the class attribute of the road
        Assert.All(streetNames.Features, f => Assert.NotNull(f.Attributes["class"]));
    }

    /// <summary>
    /// Buildings and water of the real zuerich.map decode to the corresponding source layers
    /// with the geometry intersecting the tile space.
    /// </summary>
    [Fact]
    public async Task ZurichMapLayersTest()
    {
        var vectorTile = await ConvertZurichTile(8.5417, 47.3765, 15);

        foreach (var expectedLayer in new[] { "building", "landuse", "water", "transportation", "boundary" })
        {
            var layer = vectorTile.Layers.FirstOrDefault(l => l.Name == expectedLayer);
            Assert.True(layer != null && layer.Features.Count > 0, $"Layer '{expectedLayer}' is missing or empty");
        }

        // The features of the base tiles covered by the requested tile intersect the tile space:
        // the mapsforge writer clips every way at its base zoom tile (with a small overlap at
        // the tile borders), so a feature far outside of it indicates a decoding error. Line
        // runs keep their original end points outside the tile border (for a seamless
        // continuation in the neighbouring tiles), so for them an intersection is enough.
        var tileEnvelope = new Envelope(-8, 520, -8, 520);
        foreach (var layer in vectorTile.Layers)
        {
            foreach (var feature in layer.Features)
            {
                if (feature.Geometry is LineString or MultiLineString)
                {
                    Assert.True(tileEnvelope.Intersects(feature.Geometry.EnvelopeInternal),
                        $"Line feature of layer '{layer.Name}' does not reach the tile: bbox {feature.Geometry.EnvelopeInternal}");
                    continue;
                }

                Assert.True(feature.Geometry.EnvelopeInternal.Intersects(tileEnvelope) &&
                        feature.Geometry.EnvelopeInternal.MaxX < 520 && feature.Geometry.EnvelopeInternal.MinX > -8 &&
                        feature.Geometry.EnvelopeInternal.MaxY < 520 && feature.Geometry.EnvelopeInternal.MinY > -8,
                    $"Feature of layer '{layer.Name}' is far outside of the tile: bbox {feature.Geometry.EnvelopeInternal}");
            }
        }
    }

    /// <summary>
    /// Converts the zuerich.map data of the lowest containing tile (the sub-file minimum) for
    /// the requested tile: from zoom 12 on the data is converted per overzoom tile.
    /// </summary>
    private static async Task<VecTiles.Common.Primitives.VectorTile> ConvertZurichTile(
        double longitude, double latitude, int zoom)
    {
        using var dataSource = new VecTiles.DataSources.Mapsforge.MapsforgeTileDataSource(
            [Path.Combine("Files", "zuerich.map")], 0, 21);

        var requested = ToTile(longitude, latitude, zoom);
        var provided = requested;
        while (await dataSource.GetTileAsync(provided) is not byte[] envelope && provided.Zoom > 0)
            provided = provided.Parent!;
        var data = await dataSource.GetTileAsync(provided) ?? throw new InvalidOperationException("No tile in the map file");

        var vectorTile = await new MapsforgeTileConverter().Convert(requested, provided, data)
            ?? throw new InvalidOperationException("The converter returned no vector tile");
        return vectorTile;
    }

    /// <summary>
    /// Computes the tile that contains the given position, using the same XYZ tile numbers as
    /// the tile data source.
    /// </summary>
    private static Tile ToTile(double longitude, double latitude, int zoom)
    {
        var x = (int)Math.Floor((longitude + 180.0) / 360.0 * Math.Pow(2, zoom));
        var radians = latitude * Math.PI / 180.0;
        var y = (int)Math.Floor((1.0 - Math.Log(Math.Tan(radians) + 1.0 / Math.Cos(radians)) / Math.PI) / 2.0 * Math.Pow(2, zoom));

        return new Tile(x, y, zoom);
    }

    private static Tile Tile(int x, int y, int zoom) => new(x, y, zoom);

    /// <summary>
    /// X coordinate of a longitude offset in microdegrees to the top-left corner of the tile in
    /// the 512 tile space, computed from the tile itself for the asserts.
    /// </summary>
    private static double TileXOfLongitudeDelta(long lonDelta, Tile tile)
    {
        var cornerLongitude = tile.X / Math.Pow(2, tile.Zoom) * 360.0 - 180.0;
        var longitude = cornerLongitude + lonDelta / 1_000_000.0;

        return (longitude + 180.0) / 360.0 * 512.0 * Math.Pow(2, tile.Zoom) - tile.X * 512.0;
    }

    /// <summary>
    /// Y coordinate of a latitude offset in microdegrees to the top-left corner of the tile in
    /// the 512 tile space, computed from the tile itself for the asserts.
    /// </summary>
    private static double TileYOfLatitudeDelta(long latDelta, Tile tile)
    {
        var cornerLatitude = Math.Atan(Math.Sinh(Math.PI * (1 - 2.0 * tile.Y / Math.Pow(2, tile.Zoom))));
        var latitudeRadians = cornerLatitude + latDelta / 1_000_000.0 * Math.PI / 180.0;

        return (1.0 - Math.Log(Math.Tan(Math.PI / 4.0 + latitudeRadians / 2.0)) / Math.PI) / 2.0
            * 512.0 * Math.Pow(2, tile.Zoom) - tile.Y * 512.0;
    }

    /// <summary>
    /// Add a POI to the given tile data bytes: position offsets in microdegrees relative to the
    /// tile corner, tag ids and no further optional features except the name.
    /// </summary>
    private static void AddPoint(List<byte> data, long latDelta, long lonDelta, int[]? poiTagIds, string? name = null)
    {
        data.AddRange(WriteVbeS(latDelta));
        data.AddRange(WriteVbeS(lonDelta));
        data.Add((byte)(poiTagIds?.Length ?? 0)); // layer 0, tag count in the lower half-byte
        foreach (var tagId in poiTagIds ?? [])
            data.AddRange(WriteVbeu(tagId));
        data.Add((byte)(name != null ? 0x80 : 0x00));
        if (name != null)
            AddString(data, name);
    }

    /// <summary>
    /// Add a way with the given node coordinates and optional tag ids to the given tile data
    /// bytes, single delta encoded.
    /// </summary>
    private static void AddWay(List<byte> data, List<(long Lat, long Lon)> nodes, int[]? wayTagIds)
    {
        var properties = new List<byte>();
        properties.AddRange([0x00, 0x00]); // way tile bitmask
        properties.Add((byte)(wayTagIds?.Length ?? 0)); // layer 0, tag count in the lower half-byte
        foreach (var tagId in wayTagIds ?? [])
            properties.AddRange(WriteVbeu(tagId));
        properties.Add(0x00); // no optional features

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

        data.AddRange(WriteVbeu(properties.Count + blocks.Count)); // way data size from the sub tile bitmap to the end of the way data
        data.AddRange(properties);
        data.AddRange(blocks);
    }

    /// <summary>
    /// Add a string as VBE-U length + UTF-8 bytes.
    /// </summary>
    private static void AddString(List<byte> data, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        data.AddRange(WriteVbeu(bytes.Length));
        data.AddRange(bytes);
    }

    /// <summary>
    /// Build the tile data of one base zoom tile: a zoom table with one row of POI and way
    /// counts per zoom level, the first way offset, the POI data and the way data; without
    /// debug signatures.
    /// </summary>
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

    /// <summary>
    /// Build a tile envelope with the given header values, tag tables and tiles, without debug
    /// signatures, see the remarks of the mapsforge tile data source for the layout.
    /// </summary>
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

    /// <summary>
    /// Write an unsigned VBE-U INT.
    /// </summary>
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

    /// <summary>
    /// Write a signed VBE-S INT: like the VBE-U INT with the sign in the second bit of the last
    /// byte and only six data bits in the last byte.
    /// </summary>
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

    /// <summary>
    /// A simple ITagsConverter for the tests that puts all features into one source layer and
    /// keeps the raw tags.
    /// </summary>
    private sealed class OneLayerTagsConverter(string sourceLayer) : VecTiles.Common.Interfaces.ITagsConverter
    {
        public (string SourceLayer, NetTopologySuite.Features.IAttributesTable Attributes)[] ConvertTags(
            Geometry geometry, string provisionalSourceLayer, NetTopologySuite.Features.IAttributesTable tags)
        {
            return [(sourceLayer, tags)];
        }
    }
}
