using System.Buffers.Binary;
using System.Text;
using VecTiles.Common.Enums;
using VecTiles.Common.Primitives;
using VecTiles.DataSources;
using VecTiles.DataSources.Mapsforge;

namespace VecTiles.DataSources.Tests;

/// <summary>
/// Tests for <see cref="MapsforgeTileDataSource"/>. Uses the real <c>zuerich.map</c> file and
/// synthetic files for the water tile, empty tile and debug file cases.
/// </summary>
public class MapsforgeTileDataSourceTests
{
    readonly string _path = Path.Combine("Files", "OpenMapTiles", "zuerich.map");

    /// <summary>
    /// Verifies that the header metadata of the real Zurich mapsforge file is read correctly.
    /// </summary>
    [Fact]
    public void CheckFileHeaderTest()
    {
        using var dataSource = new MapsforgeTileDataSource([_path], 0, 22);

        Assert.Equal("zuerich.map", dataSource.Name);
        Assert.Equal(3, dataSource.FileVersion);
        Assert.Equal("Mercator", dataSource.Projection);
        Assert.Equal(256, dataSource.TileSize);
        Assert.False(dataSource.IsDebugInfoAvailable);
        Assert.Equal(47.13, dataSource.MinLatitude);
        Assert.Equal(8.289999, dataSource.MinLongitude);
        Assert.Equal(47.58, dataSource.MaxLatitude);
        Assert.Equal(8.87, dataSource.MaxLongitude);
        Assert.Equal("mapsforge-map-writer-0.18.0", dataSource.CreatedBy);
        Assert.Equal(string.Empty, dataSource.Comment);
        Assert.Equal(string.Empty, dataSource.LanguagePreference);
        Assert.Null(dataSource.StartPositionLatitude);
        Assert.Null(dataSource.StartPositionLongitude);
        Assert.Null(dataSource.StartZoom);
        Assert.Equal(0, dataSource.MinZoom);
        // the maximum zoom level of the file (the creator parameter only allows a restriction)
        Assert.Equal(21, dataSource.MaxZoom);
    }

    /// <summary>
    /// Verifies that the tag string tables and the zoom intervals of the real Zurich file are
    /// read correctly. The base zoom level of a sub-file can lie anywhere within its interval.
    /// </summary>
    [Fact]
    public void CheckTagTablesAndZoomIntervalsTest()
    {
        using var dataSource = new MapsforgeTileDataSource([_path], 0, 22);

        Assert.Equal(91, dataSource.PoiTags.Count);
        Assert.Equal(279, dataSource.WayTags.Count);
        Assert.Equal("natural=tree", dataSource.PoiTags[0]);
        Assert.Equal("building=yes", dataSource.WayTags[0]);

        Assert.Equal(
            [
                new MapsforgeSubFile(5, 0, 7) { StartOffset = 6688, Size = 96966 },
                new MapsforgeSubFile(10, 8, 11) { StartOffset = 103654, Size = 1142342 },
                new MapsforgeSubFile(14, 12, 21) { StartOffset = 1245996, Size = 42466154 },
            ],
            dataSource.SubFiles);
    }

    /// <summary>
    /// Verifies that the tile data of the zoom intervals of the real Zurich file is returned at
    /// their minimum zoom levels: 0, 8 and, in the middle of the intervals zoom range, 12.
    /// </summary>
    [Fact]
    public async Task GetTileAtIntervalMinZoomTest()
    {
        using var dataSource = new MapsforgeTileDataSource([_path], 0, 22);

        // Zoom 0 is the minimum zoom of the interval [0..7] with base zoom 5; the base zoom
        // tile grid of the file is a single tile for this zoom interval
        var zoom0 = ParseEnvelope((await dataSource.GetTileAsync(ToTile(8.5417, 47.3765, 0)))!);
        Assert.Equal(0, zoom0.Flags);
        Assert.Equal(5, zoom0.BaseZoom);
        Assert.Equal(16, zoom0.FirstTileX);
        Assert.Equal(11, zoom0.FirstTileY);
        Assert.Equal(1, zoom0.Columns);
        var baseTile0 = Assert.Single(zoom0.Tiles);
        Assert.False(baseTile0.IsWater);
        Assert.True(baseTile0.Data.Length > 100);

        // Zoom 8 is the minimum zoom of the interval [8..11] with base zoom 10; the Zurich
        // main station tile covers a 2x3 rectangle of the 3x3 base zoom tile grid
        var zoom8 = ParseEnvelope((await dataSource.GetTileAsync(ToTile(8.5417, 47.3765, 8)))!);
        Assert.Equal(0, zoom8.Flags);
        Assert.Equal(10, zoom8.BaseZoom);
        Assert.Equal(536, zoom8.FirstTileX);
        Assert.Equal(357, zoom8.FirstTileY);
        Assert.Equal(2, zoom8.Columns);
        Assert.Equal(6, zoom8.Tiles.Count);
        Assert.All(zoom8.Tiles, t => Assert.False(t.IsWater));
        Assert.All(zoom8.Tiles, t => Assert.True(t.Data.Length > 32));

        // Zoom 12 is the minimum zoom of the interval [12..21] with base zoom 14
        var zoom12 = ParseEnvelope((await dataSource.GetTileAsync(ToTile(8.5417, 47.3765, 12)))!);
        Assert.Equal(0, zoom12.Flags);
        Assert.Equal(14, zoom12.BaseZoom);
        Assert.True(zoom12.Tiles.Count > 0);
    }

    /// <summary>
    /// Verifies that only the tiles at the minimum zoom levels of the zoom intervals return
    /// data directly; the tile data of the other zoom levels is generated from these tiles by
    /// the calling side via the overzoom fallback.
    /// </summary>
    [Fact]
    public async Task GetTileOutsideOfIntervalMinZoomReturnsNullTest()
    {
        using var dataSource = new MapsforgeTileDataSource([_path], 0, 22);

        // Zoom levels of the intervals above their minimum zoom level, including the base
        // zoom levels 5, 10 and 14 of the intervals
        Assert.Null(await dataSource.GetTileAsync(ToTile(8.5417, 47.3765, 1)));
        Assert.Null(await dataSource.GetTileAsync(ToTile(8.5417, 47.3765, 5)));
        Assert.Null(await dataSource.GetTileAsync(ToTile(8.5417, 47.3765, 9)));
        Assert.Null(await dataSource.GetTileAsync(ToTile(8.5417, 47.3765, 10)));
        Assert.Null(await dataSource.GetTileAsync(ToTile(8.5417, 47.3765, 11)));
        Assert.Null(await dataSource.GetTileAsync(ToTile(8.5417, 47.3765, 14)));
        Assert.Null(await dataSource.GetTileAsync(ToTile(8.5417, 47.3765, 21)));

        // Zoom level above the maximum zoom of the file
        Assert.Null(await dataSource.GetTileAsync(ToTile(8.5417, 47.3765, 22)));
    }

    /// <summary>
    /// Verifies that tiles whose base zoom tile rectangle does not intersect the bounding box
    /// of the file return null.
    /// </summary>
    [Fact]
    public async Task GetTileOutsideOfBoundingBoxTest()
    {
        using var dataSource = new MapsforgeTileDataSource([_path], 0, 22);

        // New York is outside of the bounding box of the Zurich map; at the minimum zoom
        // levels 8 and 12 of the zoom intervals the tile rectangles do not intersect the
        // tile grid of the file
        Assert.Null(await dataSource.GetTileAsync(ToTile(-74.0060, 40.7128, 8)));
        Assert.Null(await dataSource.GetTileAsync(ToTile(-74.0060, 40.7128, 12)));
    }

    /// <summary>
    /// Verifies that invalid tiles return null.
    /// </summary>
    [Fact]
    public async Task GetInvalidTileTest()
    {
        using var dataSource = new MapsforgeTileDataSource([_path], 0, 22);

        Assert.Null(await dataSource.GetTileAsync(null!));
        Assert.Null(await dataSource.GetTileAsync(new Tile(-1, 0, 1)));
        Assert.Null(await dataSource.GetTileAsync(new Tile(0, 0, 30)));
    }

    /// <summary>
    /// Verifies that the zoom range of the data source is restricted by the constructor parameters.
    /// </summary>
    [Fact]
    public void ZoomRangeRestrictionTest()
    {
        using var dataSource = new MapsforgeTileDataSource([_path], 10, 20);

        Assert.Equal(10, dataSource.MinZoom);
        Assert.Equal(20, dataSource.MaxZoom);

        // Zoom range of the constructor and of the file do not intersect
        Assert.Throws<ArgumentException>(() => new MapsforgeTileDataSource([_path], 30, 40));
    }

    /// <summary>
    /// Verifies that the source is created by the <see cref="DefaultTileDataSourceFactory"/>
    /// and that the scheme prefix is stripped.
    /// </summary>
    [Fact]
    public void FactoryCreatesMapsforgeSourceTest()
    {
        var dataSource = DefaultTileDataSourceFactory.CreateTileDataSource(["mapsforge://" + _path], 0, 22);

        var mapsforgeDataSource = Assert.IsType<MapsforgeTileDataSource>(dataSource);
        Assert.Equal(_path, mapsforgeDataSource.Path);
        Assert.Equal(SourceType.Vector, mapsforgeDataSource.SourceType);
        Assert.Equal(Scheme.Xyz, mapsforgeDataSource.Scheme);
    }

    /// <summary>
    /// Verifies that the constructor and file format validation throws the expected exceptions.
    /// </summary>
    [Fact]
    public void ConstructorValidationTest()
    {
        Assert.Throws<ArgumentException>(() => new MapsforgeTileDataSource([], 0, 22));
        Assert.Throws<FileNotFoundException>(() => new MapsforgeTileDataSource(["does_not_exist.map"], 0, 22));

        var tempFile = GetTempFileName("invalid_map");
        try
        {
            File.WriteAllBytes(tempFile, [0x01, 0x02, 0x03]);
            Assert.Throws<InvalidDataException>(() => new MapsforgeTileDataSource([tempFile], 0, 22));

            WriteMapFile(tempFile, debug: false, fileVersion: 2, ["poi=tag"], ["way=tag"], baseZoom: 2, minZoom: 0, maxZoom: 3,
                minLat: -20, minLon: -20, maxLat: 20, maxLon: 20, [new(1, 1, false, [0xaa])]);
            Assert.Throws<NotSupportedException>(() => new MapsforgeTileDataSource([tempFile], 0, 22));

            WriteMapFile(tempFile, debug: false, fileVersion: 6, ["poi=tag"], ["way=tag"], baseZoom: 2, minZoom: 0, maxZoom: 3,
                minLat: -20, minLon: -20, maxLat: 20, maxLon: 20, [new(1, 1, false, [0xaa])]);
            Assert.Throws<NotSupportedException>(() => new MapsforgeTileDataSource([tempFile], 0, 22));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies the tile lookup of a synthetic file with a 2x2 tile grid covering the four tile
    /// cases: normal tile, water tile, empty tile and the end of the tile data segment. As the
    /// base zoom (4) lies above the minimum zoom (3) of the interval, the minimum zoom tile
    /// covers a 2x2 rectangle of base zoom tiles.
    /// </summary>
    [Fact]
    public async Task SyntheticFileLookupTest()
    {
        var tempFile = GetTempFileName("synthetic_map");
        try
        {
            // Synthetic file with one sub-file for the zoom interval [3..4] with base zoom 4;
            // the bounding box 10..40 covers the base zoom tile grid (8,6)..(9,7), which lies
            // completely within the minimum zoom tile (4,3)
            WriteMapFile(tempFile, debug: false, fileVersion: 5, ["poi=tag"], ["way=tag"], baseZoom: 4, minZoom: 3, maxZoom: 4,
                minLat: 10, minLon: 10, maxLat: 40, maxLon: 40,
                [
                    new(8, 6, false, [0xaa, 0xbb, 0xcc]), // tile (8,6): normal tile with data
                    new(9, 6, true, []),                  // tile (9,6): water tile without data
                    new(8, 7, false, []),                 // tile (8,7): empty tile
                    new(9, 7, false, [0xdd, 0xee]),       // tile (9,7): normal tile with data
                ]);

            using var dataSource = new MapsforgeTileDataSource(["mapsforge:" + tempFile], 3, 4);

            // The minimum zoom tile returns the tile data of all covered base zoom tiles
            var envelope = ParseEnvelope((await dataSource.GetTileAsync(new Tile(4, 3, 3)))!);
            Assert.Equal(0, envelope.Flags);
            Assert.Equal(4, envelope.BaseZoom);
            Assert.Equal(8, envelope.FirstTileX);
            Assert.Equal(6, envelope.FirstTileY);
            Assert.Equal(2, envelope.Columns);
            Assert.Equal(
                [
                    new EnvelopeTile(false, [0xaa, 0xbb, 0xcc]),
                    new EnvelopeTile(true, []),
                    new EnvelopeTile(false, []),
                    new EnvelopeTile(false, [0xdd, 0xee]),
                ],
                envelope.Tiles);

            // Tiles at the zoom level of the base zoom are left to the overzoom fallback
            Assert.Null(await dataSource.GetTileAsync(new Tile(8, 6, 4)));

            // Tile next to the tile grid of the file
            Assert.Null(await dataSource.GetTileAsync(new Tile(5, 3, 3)));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that a tile that is completely covered by water tiles returns the water meta
    /// flags byte only.
    /// </summary>
    [Fact]
    public async Task WaterTileTest()
    {
        var tempFile = GetTempFileName("water_map");
        try
        {
            // Synthetic file with a single base zoom tile (the bounding box lies within one
            // tile of the 2x2 tile zoom grid), storing a water tile without data
            WriteMapFile(tempFile, debug: false, fileVersion: 5, ["poi=tag"], ["way=tag"], baseZoom: 2, minZoom: 2, maxZoom: 2,
                minLat: 22, minLon: 22, maxLat: 28, maxLon: 28, [new(2, 1, true, [])]);

            using var dataSource = new MapsforgeTileDataSource([tempFile], 2, 2);

            Assert.Equal([0x01], await dataSource.GetTileAsync(new Tile(2, 1, 2)));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that a tile without data is not rendered by the converter but is left to the
    /// overzoom fallback of the calling side.
    /// </summary>
    [Fact]
    public async Task EmptyTileTest()
    {
        var tempFile = GetTempFileName("empty_map");
        try
        {
            // Synthetic file with a single base zoom tile, storing an empty tile without data
            // and without the water flag
            WriteMapFile(tempFile, debug: false, fileVersion: 5, ["poi=tag"], ["way=tag"], baseZoom: 2, minZoom: 2, maxZoom: 2,
                minLat: 22, minLon: 22, maxLat: 28, maxLon: 28, [new(2, 1, false, [])]);

            using var dataSource = new MapsforgeTileDataSource([tempFile], 2, 2);

            Assert.Null(await dataSource.GetTileAsync(new Tile(2, 1, 2)));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that a file with debug information returns the tile data including the signatures,
    /// as stored in the file (debug information is passed through).
    /// </summary>
    [Fact]
    public async Task SyntheticFileWithDebugInfoTest()
    {
        var tempFile = GetTempFileName("synthetic_debug_map");
        try
        {
            // Synthetic file with one tile, storing debug signatures in the index and in the tile
            WriteMapFile(tempFile, debug: true, fileVersion: 5, ["poi=tag"], ["way=tag"], baseZoom: 2, minZoom: 1, maxZoom: 3,
                minLat: 1, minLon: -45, maxLat: 44, maxLon: -1, [new(1, 1, false, [0xaa, 0xbb, 0xcc])]);

            using var dataSource = new MapsforgeTileDataSource([tempFile], 0, 22);

            Assert.True(dataSource.IsDebugInfoAvailable);

            // The minimum zoom tile (0,0,1) covers the single base zoom tile (1,1)
            var envelope = ParseEnvelope((await dataSource.GetTileAsync(new Tile(0, 0, 1)))!);
            Assert.Equal(0, envelope.Flags);
            Assert.Equal(2, envelope.BaseZoom);
            Assert.Equal(1, envelope.FirstTileX);
            Assert.Equal(1, envelope.FirstTileY);
            var envelopeTile = Assert.Single(envelope.Tiles);
            Assert.False(envelopeTile.IsWater);

            var expectedSignature = Encoding.ASCII.GetBytes("###TileStart1,1###".PadRight(32));
            var expectedTileData = new byte[expectedSignature.Length + 3];
            expectedSignature.CopyTo(expectedTileData, 0);
            new byte[] { 0xaa, 0xbb, 0xcc }.CopyTo(expectedTileData, expectedSignature.Length);
            Assert.Equal(expectedTileData, envelopeTile.Data);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Writes a minimal Mapsforge map file with a single zoom interval sub-file, containing one
    /// tile per entry of the given tile list (row-wise from the top-left tile of the tile grid).
    /// The bounding box must define the tile grid at the base zoom level, so that the row-wise
    /// tile list matches the index entries of the file.
    /// </summary>
    private static void WriteMapFile(string path, bool debug, int fileVersion, string[] poiTags, string[] wayTags,
        int baseZoom, int minZoom, int maxZoom, int minLat, int minLon, int maxLat, int maxLon, (int TileX, int TileY, bool Water, byte[] Data)[] tiles)
    {
        // The tile data segment of the sub-file: the offset of each tile is the accumulated data
        // length of the previous tiles, so the offset of empty and water tiles equals the offset
        // of the following tile
        var indexSignatureLength = debug ? 16 : 0;
        var tileSignatureLength = debug ? 32 : 0;
        var subFileSize = (long)indexSignatureLength + (long)tiles.Length * 5 +
            (long)tiles.Length * tileSignatureLength + tiles.Sum(t => (long)t.Data.Length);

        var data = new MemoryStream();
        if (debug)
            data.Write("+++IndexStart+++"u8);

        long tileOffset = indexSignatureLength + tiles.Length * 5;
        foreach (var tile in tiles)
        {
            // Tile index entry: 40th bit = water flag, lower 39 bits = tile offset, big endian
            var entry = tileOffset | (tile.Water ? 1L << 39 : 0L);
            data.Write([(byte)(entry >> 32), (byte)(entry >> 24), (byte)(entry >> 16), (byte)(entry >> 8), (byte)entry]);
            tileOffset += tile.Data.Length;
        }

        foreach (var tile in tiles)
        {
            if (debug)
                data.Write(Encoding.ASCII.GetBytes($"###TileStart{tile.TileX},{tile.TileY}###".PadRight(32)));

            data.Write(tile.Data);
        }

        // File header
        using var header = new MemoryStream();
        header.Write("mapsforge binary OSM"u8);
        WriteInt32BE(header, 0);   // header size, patched below
        WriteInt32BE(header, fileVersion);
        WriteInt64BE(header, 0);   // file size, patched below
        WriteInt64BE(header, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        WriteInt32BE(header, minLat * 1_000_000);
        WriteInt32BE(header, minLon * 1_000_000);
        WriteInt32BE(header, maxLat * 1_000_000);
        WriteInt32BE(header, maxLon * 1_000_000);
        WriteUInt16BE(header, 256);
        var projection = debug ? "MercatorProjection" : "Mercator";
        var projectionBytes = Encoding.UTF8.GetBytes(projection);
        WriteVbeu(header, projectionBytes.Length);
        header.Write(projectionBytes);
        header.WriteByte(debug ? (byte)0x80 : (byte)0x00);

        WriteUInt16BE(header, (ushort)poiTags.Length);
        foreach (var tag in poiTags)
            WriteString(header, tag);

        WriteUInt16BE(header, (ushort)wayTags.Length);
        foreach (var tag in wayTags)
            WriteString(header, tag);

        header.WriteByte(1);

        header.WriteByte((byte)baseZoom);
        header.WriteByte((byte)minZoom);
        header.WriteByte((byte)maxZoom);
        var startOffsetPos = (int)header.Length;
        WriteInt64BE(header, 0);   // start offset, patched below
        WriteInt64BE(header, subFileSize);

        var file = header.ToArray();

        // The header size counts all header bytes behind the magic bytes, including the
        // header size field itself
        var headerSize = file.Length - 20;
        var startOffset = 20 + headerSize;

        // Patch the header size, the file size and the start offset fields
        BinaryPrimitives.WriteInt32BigEndian(file.AsSpan(20), headerSize);
        BinaryPrimitives.WriteInt64BigEndian(file.AsSpan(28), startOffset + subFileSize);
        BinaryPrimitives.WriteInt64BigEndian(file.AsSpan(startOffsetPos), startOffset);

        using var outputStream = File.Create(path);
        outputStream.Write(file);
        data.Position = 0;
        data.CopyTo(outputStream);
    }

    private static string GetTempFileName(string fileName) => Path.Combine(Path.GetTempPath(), $"{fileName}_{Guid.NewGuid()}.map");

    private static void WriteInt32BE(Stream stream, int value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buffer, value);
        stream.Write(buffer);
    }

    private static void WriteInt64BE(Stream stream, long value)
    {
        Span<byte> buffer = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(buffer, value);
        stream.Write(buffer);
    }

    private static void WriteUInt16BE(Stream stream, ushort value)
    {
        Span<byte> buffer = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(buffer, value);
        stream.Write(buffer);
    }

    private static void WriteString(Stream stream, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        WriteVbeu(stream, bytes.Length);
        stream.Write(bytes);
    }

    private static void WriteVbeu(Stream stream, long value)
    {
        do
        {
            var currentByte = (byte)(value & 0x7f);
            value >>= 7;
            if (value != 0)
                currentByte |= 0x80;

            stream.WriteByte(currentByte);
        }
        while (value != 0);
    }

    /// <summary>
    /// A tile of a tile data envelope as returned by <see cref="MapsforgeTileDataSource.GetTileAsync"/>.
    /// </summary>
    private sealed record EnvelopeTile(bool IsWater, byte[] Data)
    {
        public bool Equals(EnvelopeTile? other) =>
            other is not null && IsWater == other.IsWater && Data.AsSpan().SequenceEqual(other.Data);

        public override int GetHashCode() => HashCode.Combine(IsWater, Data.Length);
    }

    private sealed record Envelope(byte Flags, int BaseZoom, int SubFileMinZoom, int SubFileMaxZoom,
        int FirstTileX, int FirstTileY, int Columns, string[] PoiTags, string[] WayTags, List<EnvelopeTile> Tiles);

    /// <summary>
    /// Decodes the envelope with the tile data of the covered base zoom tiles that is returned
    /// by <see cref="MapsforgeTileDataSource.GetTileAsync"/>, see the class remarks there.
    /// </summary>
    private static Envelope ParseEnvelope(byte[] envelope)
    {
        var pos = 0;
        var flags = envelope[pos++];
        if (flags != 0)
            return new Envelope(flags, -1, -1, -1, -1, -1, -1, [], [], []);

        var baseZoom = (int)ReadVbeu(envelope, ref pos);
        var subFileMinZoom = (int)ReadVbeu(envelope, ref pos);
        var subFileMaxZoom = (int)ReadVbeu(envelope, ref pos);
        var firstTileX = (int)ReadVbeu(envelope, ref pos);
        var firstTileY = (int)ReadVbeu(envelope, ref pos);
        var columns = (int)ReadVbeu(envelope, ref pos);
        var count = (int)ReadVbeu(envelope, ref pos);
        var poiTags = ReadStringTable(envelope, ref pos);
        var wayTags = ReadStringTable(envelope, ref pos);
        var tiles = new List<EnvelopeTile>(count);
        for (var i = 0; i < count; i++)
        {
            var tileFlags = envelope[pos++];
            var length = (int)ReadVbeu(envelope, ref pos);
            var data = new byte[length];
            Array.Copy(envelope, pos, data, 0, length);
            pos += length;
            tiles.Add(new EnvelopeTile((tileFlags & 0x01) != 0, data));
        }

        return new Envelope(flags, baseZoom, subFileMinZoom, subFileMaxZoom, firstTileX, firstTileY, columns,
            poiTags, wayTags, tiles);
    }

    private static string[] ReadStringTable(byte[] data, ref int pos)
    {
        var count = (int)ReadVbeu(data, ref pos);
        var strings = new string[count];
        for (var i = 0; i < count; i++)
        {
            var length = (int)ReadVbeu(data, ref pos);
            strings[i] = Encoding.UTF8.GetString(data, pos, length);
            pos += length;
        }

        return strings;
    }

    private static long ReadVbeu(byte[] data, ref int pos)
    {
        long value = 0;
        var shift = 0;
        byte currentByte;
        do
        {
            currentByte = data[pos++];
            value |= (long)(currentByte & 0x7f) << shift;
            shift += 7;
        }
        while ((currentByte & 0x80) != 0);

        return value;
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
}
