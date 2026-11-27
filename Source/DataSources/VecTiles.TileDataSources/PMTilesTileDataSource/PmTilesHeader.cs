using System.Buffers.Binary;

namespace VecTiles.DataSources.PmTiles;

/// <summary>
/// The 127 byte header at the start of a PMTiles archive
/// (see https://github.com/protomaps/PMTiles/blob/main/spec/v3/spec.md, chapter 3).
/// All multi byte fields are little endian.
/// </summary>
public class PmTilesHeader
{
    /// <summary>
    /// Length of the PMTiles header in bytes
    /// </summary>
    public const int Length = 127;

    /// <summary>
    /// Magic number at the start of every PMTiles archive
    /// </summary>
    public const string Magic = "PMTiles";

    /// <summary>
    /// Version of the PMTiles specification this implementation reads
    /// </summary>
    public const byte Version = 3;

    /// <summary>
    /// Offset of the root directory, relative to the start of the archive
    /// </summary>
    public ulong RootDirectoryOffset { get; private set; }

    /// <summary>
    /// Length of the root directory in bytes
    /// </summary>
    public ulong RootDirectoryLength { get; private set; }

    /// <summary>
    /// Offset of the JSON metadata, relative to the start of the archive
    /// </summary>
    public ulong MetadataOffset { get; private set; }

    /// <summary>
    /// Length of the JSON metadata in bytes
    /// </summary>
    public ulong MetadataLength { get; private set; }

    /// <summary>
    /// Offset of the leaf directories, relative to the start of the archive
    /// </summary>
    public ulong LeafDirectoriesOffset { get; private set; }

    /// <summary>
    /// Accumulated length of all leaf directories in bytes
    /// </summary>
    public ulong LeafDirectoriesLength { get; private set; }

    /// <summary>
    /// Offset of the tile data section, relative to the start of the archive
    /// </summary>
    public ulong TileDataOffset { get; private set; }

    /// <summary>
    /// Accumulated length of all tiles in the tile data section in bytes
    /// </summary>
    public ulong TileDataLength { get; private set; }

    /// <summary>
    /// Whether the tiles in the tile data section are ordered by their tile id
    /// </summary>
    public bool Clustered { get; private set; }

    /// <summary>
    /// Compression of the root directory, the JSON metadata and the leaf directories
    /// </summary>
    public PmTilesCompression InternalCompression { get; private set; }

    /// <summary>
    /// Compression of the tiles
    /// </summary>
    public PmTilesCompression TileCompression { get; private set; }

    /// <summary>
    /// Type of the tiles in the archive
    /// </summary>
    public PmTilesTileType TileType { get; private set; }

    /// <summary>
    /// Minimum zoom of the tiles
    /// </summary>
    public int MinZoom { get; private set; }

    /// <summary>
    /// Maximum zoom of the tiles
    /// </summary>
    public int MaxZoom { get; private set; }

    /// <summary>
    /// Minimum longitude of the bounds in degrees, null if the header holds no usable value
    /// </summary>
    public double? MinLongitude { get; private set; }

    /// <summary>
    /// Minimum latitude of the bounds in degrees, null if the header holds no usable value
    /// </summary>
    public double? MinLatitude { get; private set; }

    /// <summary>
    /// Maximum longitude of the bounds in degrees, null if the header holds no usable value
    /// </summary>
    public double? MaxLongitude { get; private set; }

    /// <summary>
    /// Maximum latitude of the bounds in degrees, null if the header holds no usable value
    /// </summary>
    public double? MaxLatitude { get; private set; }

    /// <summary>
    /// Center zoom of the tiles, null if the header holds no usable value
    /// </summary>
    public int? CenterZoom { get; private set; }

    /// <summary>
    /// Longitude of the center position in degrees, null if the header holds no usable value
    /// </summary>
    public double? CenterLongitude { get; private set; }

    /// <summary>
    /// Latitude of the center position in degrees, null if the header holds no usable value
    /// </summary>
    public double? CenterLatitude { get; private set; }

    /// <summary>
    /// Parses the header from the first <see cref="Length"/> bytes of a PMTiles archive.
    /// </summary>
    /// <param name="data">The first bytes of the archive</param>
    /// <returns>The parsed header</returns>
    /// <exception cref="InvalidDataException">Thrown when the data is not a PMTiles v3 header</exception>
    public static PmTilesHeader Parse(byte[] data)
    {
        if (data.Length < Length)
            throw new InvalidDataException($"A PMTiles header needs at least {Length} bytes, got {data.Length}");

        if (System.Text.Encoding.ASCII.GetString(data, 0, Magic.Length) != Magic)
            throw new InvalidDataException("The file is not a PMTiles archive (wrong magic number)");

        var version = data[7];
        if (version != Version)
            throw new InvalidDataException($"Unsupported PMTiles version {version}, version {Version} is expected");

        var header = new PmTilesHeader
        {
            RootDirectoryOffset = BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(8)),
            RootDirectoryLength = BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(16)),
            MetadataOffset = BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(24)),
            MetadataLength = BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(32)),
            LeafDirectoriesOffset = BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(40)),
            LeafDirectoriesLength = BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(48)),
            TileDataOffset = BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(56)),
            TileDataLength = BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(64)),
            Clustered = data[96] != 0,
            InternalCompression = (PmTilesCompression)data[97],
            TileCompression = (PmTilesCompression)data[98],
            TileType = (PmTilesTileType)data[99],
            MinZoom = data[100],
            MaxZoom = data[101]
        };

        // Positions are little endian 32 bit signed integers in 1/10000000 degrees,
        // a longitude first and a latitude second, each in an 8 byte field. Older writers
        // stored positions with less precision or left them out, so unusable values
        // (outside their valid ranges) are ignored.
        (header.MinLongitude, header.MinLatitude) = ReadPosition(data, 101);
        (header.MaxLongitude, header.MaxLatitude) = ReadPosition(data, 109);
        header.CenterZoom = data[117] <= 31 ? data[117] : null;
        (header.CenterLongitude, header.CenterLatitude) = ReadPosition(data, 118);

        return header;
    }

    /// <summary>
    /// Reads an 8 byte position (longitude and latitude) from the header.
    /// </summary>
    /// <param name="data">The header bytes</param>
    /// <param name="offset">Offset of the position within the header</param>
    /// <returns>Longitude and latitude in degrees, both null when the stored value is unusable</returns>
    private static (double? Longitude, double? Latitude) ReadPosition(byte[] data, int offset)
    {
        var longitude = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset)) / 10_000_000.0;
        var latitude = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset + 4)) / 10_000_000.0;

        if (longitude is < -180 or > 180 || latitude is < -90 or > 90)
            return (null, null);

        return (longitude, latitude);
    }
}
