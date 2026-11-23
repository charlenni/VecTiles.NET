using System.Buffers.Binary;
using System.Text;
using Microsoft.Win32.SafeHandles;
using VecTiles.Common.Enums;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;

namespace VecTiles.DataSources.Mapsforge;

/// <summary>
/// Tile data source for Mapsforge binary map files, as specified in
/// https://github.com/mapsforge/mapsforge/blob/master/docs/Specification-Binary-Map-File.md.
/// It reads the map file structure (file header, zoom interval sub-files, tile index segment)
/// and returns the raw tile data of a tile as byte[]. The interpretation of the tile payload
/// (POIs, ways, tag ids, delta encoded coordinates) is left to an <see cref="ITileConverter"/>.
/// </summary>
/// <remarks>
/// A Mapsforge zoom interval stores all its map data in the tile grid of the intervals base
/// zoom level (<see cref="MapsforgeSubFile.BaseZoom"/>), which may lie anywhere within the
/// intervals zoom range. This data source serves that data at the <b>minimum zoom level</b> of
/// the interval (<see cref="MapsforgeSubFile.MinZoom"/>): the lowest zoom level of the interval
/// provides the data and all higher zoom levels — within the interval and above it up to
/// <see cref="MaxZoom"/> — are generated from these tiles by the calling side via the overzoom
/// fallback. A tile at the minimum zoom level generally covers several tiles of the base zoom
/// tile grid, so the returned byte[] is an envelope with the tile data of <b>all</b> base grid
/// tiles covered by the requested tile (big endian order, as used throughout the file):
/// <list type="number">
///   <item>1 byte meta flags: 0x01 = the requested tile is completely covered by water tiles;
///     no further bytes follow in this case. Otherwise 0x00.</item>
///   <item>VBE-U INT: base zoom level of the sub-file; the tile data refers to the tile grid
///     of this zoom level and a converter scales it to the zoom of the requested tile.</item>
///   <item>VBE-U INT: minimum zoom level of the sub-file (first row of the zoom table).</item>
///   <item>VBE-U INT: maximum zoom level of the sub-file (number of rows of the zoom table is
///     maxZoom - minZoom + 1).</item>
///   <item>VBE-U INT: X tile number of the first covered base zoom tile, in the tile grid of
///     the map file (which covers its bounding box, row-wise from the top-left tile).</item>
///   <item>VBE-U INT: Y tile number of the first covered base zoom tile.</item>
///   <item>VBE-U INT: number of covered tile columns per row.</item>
///   <item>VBE-U INT: number of covered tiles, row-wise in the order of the tile index.</item>
///   <item>POI tag string table of the file header: VBE-U INT number of tags, followed by the
///     tags in the same order and encoding as in the file header (VBE-U INT length + UTF-8). The
///     tag id values in the tile payload refer to these strings by index.</item>
///   <item>Way tag string table of the file header: encoded like the POI tag string table.</item>
///   <item>per covered tile: 1 byte meta flags (0x01 = water tile, the flag comes from the
///     tile index entry and is not part of the tile data itself), VBE-U INT length of the
///     tile data, followed by the tile data exactly as stored in the tile data segment: the
///     tile header (zoom table and first way offset), the POI data and the way data, each
///     preceded by its 32-byte debug signature when the file contains debug information
///     (see <see cref="IsDebugInfoAvailable"/>).</item>
/// </list>
/// The tile payload is passed through to the converter uninterpreted; carrying the sub-file
/// zoom range (needed to address the rows of the per-tile zoom table) and the tag string tables
/// (needed to resolve the tag id values of POIs and ways) in the envelope keeps the tile payload
/// free from any interpretation by this data source.
/// </remarks>
public class MapsforgeTileDataSource : ITileDataSource, IDisposable
{
    private const string MagicBytes = "mapsforge binary OSM";
    private const string MapsforgeSchemePrefix = "mapsforge:";
    private const int MagicByteLength = 20;
    private const int ZoomIntervalConfigSize = 19;
    private const int IndexEntrySize = 5;
    private const int IndexHeaderSignatureLength = 16;
    private const byte TileMetaFlagWater = 0x01;

    private readonly List<MapsforgeSubFile> _subFiles = [];
    private readonly SafeFileHandle _fileHandle;
    private readonly EnvelopeCache _envelopeCache = new(4);
    private bool _disposed;

    ///  <summary>
    ///  Initializes a new instance of the <see cref="MapsforgeTileDataSource"/> class based on the specified map file.
    ///  </summary>
    ///  <param name="sources">List of possible sources; only the first one is used.
    ///  A source is a path to a Mapsforge map file, optionally prefixed with 'mapsforge://' or 'mapsforge:'.</param>
    ///  <param name="minZoom">Minimum zoom of the source, used to restrict the zoom range of the file</param>
    ///  <param name="maxZoom">Maximum zoom of the source, used to restrict the zoom range of the file</param>
    ///  <exception cref="ArgumentException">Thrown when no source is given or the zoom range does not intersect the zoom range of the file</exception>
    ///  <exception cref="FileNotFoundException">Thrown when the map file does not exist</exception>
    ///  <exception cref="InvalidDataException">Thrown when the file is not a Mapsforge map file or it is corrupt</exception>
    ///  <exception cref="NotSupportedException">Thrown when the Mapsforge file format version or projection is not supported</exception>
    public MapsforgeTileDataSource(string[] sources, int minZoom, int maxZoom)
    {
        if (sources is null || sources.Length == 0)
            throw new ArgumentException("At least one mapsforge source must be provided", nameof(sources));

        // Only the first source is used, matching the behavior of the other tile data sources
        Path = StripScheme(sources[0]);

        if (!File.Exists(Path))
            throw new FileNotFoundException($"The mapsforge map file does not exist: '{Path}'", Path);

        _fileHandle = File.OpenHandle(Path, FileMode.Open, FileAccess.Read, FileShare.Read, FileOptions.Asynchronous);

        try
        {
            ParseFileHeader();

            // Restrict the zoom range of the file by the zoom range given in the parameters.
            // Above the file's maximum zoom level the tile data of the highest zoom interval is
            // overzoomed by the calling side, below the file's minimum zoom level no data exists.
            MinZoom = Math.Max(minZoom, _subFiles.Min(s => s.MinZoom));
            MaxZoom = Math.Min(maxZoom, _subFiles.Max(s => s.MaxZoom));

            if (MinZoom > MaxZoom)
                throw new ArgumentException(
                    $"The zoom range {minZoom}-{maxZoom} passed to the data source does not intersect " +
                    $"the zoom range {_subFiles.Min(s => s.MinZoom)}-{_subFiles.Max(s => s.MaxZoom)} of the mapsforge file");
        }
        catch
        {
            _fileHandle.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Gets or sets the attribution text of the map file. The Mapsforge file format has no
    /// attribution field, so it has to be set manually if needed (e.g. OpenStreetMap attribution).
    /// </summary>
    public string Attribution { get; set; } = string.Empty;

    ///  <summary>
    ///  Minimum zoom level of the source; restricted by the zoom range given to the constructor.
    ///  </summary>
    public int MinZoom { get; private set; }

    ///  <summary>
    ///  Maximum zoom level of the source; restricted by the zoom range given to the constructor.
    ///  </summary>
    public int MaxZoom { get; private set; }

    ///  <summary>
    ///  Mapsforge map files contain vector data.
    ///  </summary>
    public SourceType SourceType { get; } = SourceType.Vector;

    ///  <summary>
    ///  Mapsforge tile coordinates follow the XYZ scheme (tile origin at top-left).
    ///  </summary>
    public Scheme Scheme { get; } = Scheme.Xyz;

    /// <summary>
    /// Path of the map file.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Version of the Mapsforge binary file format of the map file.
    /// </summary>
    public int FileVersion { get; private set; }

    /// <summary>
    /// Date of creation of the map file.
    /// </summary>
    public DateTime DateOfCreation { get; private set; }

    /// <summary>
    /// Minimal latitude of the bounding box of the map file in degrees.
    /// </summary>
    public double MinLatitude { get; private set; }

    /// <summary>
    /// Minimal longitude of the bounding box of the map file in degrees.
    /// </summary>
    public double MinLongitude { get; private set; }

    /// <summary>
    /// Maximal latitude of the bounding box of the map file in degrees.
    /// </summary>
    public double MaxLatitude { get; private set; }

    /// <summary>
    /// Maximal longitude of the bounding box of the map file in degrees.
    /// </summary>
    public double MaxLongitude { get; private set; }

    /// <summary>
    /// Tile size in pixels used by the map file (e.g. 256).
    /// </summary>
    public int TileSize { get; private set; }

    /// <summary>
    /// Name of the projection used to create the map file, e.g. 'MercatorProjection'.
    /// </summary>
    public string Projection { get; private set; } = string.Empty;

    /// <summary>
    /// True, when the map file contains debug information (32-byte signatures in the tile
    /// index and in the tile data). A converter has to skip these signatures.
    /// </summary>
    public bool IsDebugInfoAvailable { get; private set; }

    /// <summary>
    /// Preferred languages for names as defined in the file header, string.Empty when not available.
    /// </summary>
    public string LanguagePreference { get; private set; } = string.Empty;

    /// <summary>
    /// Comment stored in the file header, string.Empty when not available.
    /// </summary>
    public string Comment { get; private set; } = string.Empty;

    /// <summary>
    /// Name of the application which created the file, read from the file header.
    /// </summary>
    public string CreatedBy { get; private set; } = string.Empty;

    /// <summary>
    /// Latitude in degrees of the map start position stored in the file header, null when not available.
    /// </summary>
    public double? StartPositionLatitude { get; private set; }

    /// <summary>
    /// Longitude in degrees of the map start position stored in the file header, null when not available.
    /// </summary>
    public double? StartPositionLongitude { get; private set; }

    /// <summary>
    /// Zoom level of the map at first load stored in the file header, null when not available.
    /// </summary>
    public int? StartZoom { get; private set; }

    /// <summary>
    /// POI tag names of the map file. The tag ids of the POIs in the tile data refer to these
    /// tags by index, starting with 0.
    /// </summary>
    public IReadOnlyList<string> PoiTags { get; private set; } = [];

    /// <summary>
    /// Way tag names of the map file. The tag ids of the ways in the tile data refer to these
    /// tags by index, starting with 0.
    /// </summary>
    public IReadOnlyList<string> WayTags { get; private set; } = [];

    /// <summary>
    /// Zoom intervals of the map file, one sub-file per interval.
    /// </summary>
    public IReadOnlyList<MapsforgeSubFile> SubFiles => _subFiles;

    ///  <summary>
    ///  Get the tile data for a tile. Data is provided only for tiles at the minimum zoom level
    ///  of a zoom interval of the file, see the class remarks for the layout of the returned
    ///  byte[] and how the tile data of the other zoom levels is generated from it by the
    ///  calling side. Tiles outside of the bounding box of the file return null.
    ///  </summary>
    ///  <param name="tile">Tile to get the data for</param>
    ///  <returns>Tile data or null if it is not available</returns>
    public async Task<byte[]?> GetTileAsync(Tile tile)
    {
        if (tile is null || !tile.IsValid)
            return null;

        var subFile = _subFiles.FirstOrDefault(s => s.MinZoom <= tile.Zoom && tile.Zoom <= s.MaxZoom);
        if (subFile is null)
            return null;

        // All map data of a zoom interval is stored in the tile grid of the intervals base zoom
        // level. The data source serves it at the minimum zoom level of the interval: this is
        // the lowest zoom level that has data, all higher zoom levels of the interval are
        // generated from these tiles by the calling side (e.g. the overzoom fallback of the
        // VectorTileDataSource). The base zoom level may lie above the minimum zoom level, so
        // the tile requested here generally covers several tiles of the base zoom tile grid
        // and the tile data of all of them is returned (see the class remarks).
        if (tile.Zoom != subFile.MinZoom)
            return null;

        var grid = GetTileGridRect(subFile);
        var tileSpan = 1 << (subFile.BaseZoom - tile.Zoom);
        var firstTileX = Math.Max(grid.FirstTileX, tile.X * tileSpan);
        var firstTileY = Math.Max(grid.FirstTileY, tile.Y * tileSpan);
        var lastTileX = Math.Min(grid.FirstTileX + grid.Columns - 1, (tile.X + 1) * tileSpan - 1);
        var lastTileY = Math.Min(grid.FirstTileY + grid.Rows - 1, (tile.Y + 1) * tileSpan - 1);

        if (firstTileX > lastTileX || firstTileY > lastTileY)
            // The tile is outside of the bounding box of the map file
            return null;

        // Overzoom requests of the same parent tile (e.g. the consecutive tile requests of a
        // renderer) all fall back to the same envelope; rebuilding it costs the read and copy
        // of all covered tile data, so the last few envelopes are cached
        var cachedEnvelope = _envelopeCache.Get(tile.Id);
        if (cachedEnvelope is not null)
            return cachedEnvelope;

        var columns = lastTileX - firstTileX + 1;
        var rows = lastTileY - firstTileY + 1;
        var coveredTiles = new (bool IsWaterTile, byte[] Data)[columns * rows];
        var allWater = true;
        var hasData = false;

        var indexStart = subFile.StartOffset + (IsDebugInfoAvailable ? IndexHeaderSignatureLength : 0);

        for (var row = 0; row < rows; row++)
        {
            var entryIndex = (firstTileY + row - grid.FirstTileY) * grid.Columns + (firstTileX - grid.FirstTileX);
            var entryPosition = indexStart + (long)entryIndex * IndexEntrySize;

            // Read one index entry per covered tile plus the entry following the last covered
            // tile of the row, whose offset ends the tile data of the last covered tile. When
            // the row ends with the last tile of the tile grid, the tile data segment ends
            // with it instead (there is no following index entry)
            var rowEndsTheTileGrid = entryIndex + columns >= grid.Columns * grid.Rows;
            var entries = await ReadBytesAsync(entryPosition, (columns + (rowEndsTheTileGrid ? 0 : 1)) * IndexEntrySize).ConfigureAwait(false);

            for (var column = 0; column < columns; column++)
            {
                var entry = entries.AsSpan(column * IndexEntrySize, IndexEntrySize);
                var isWaterTile = (entry[0] & 0x80) != 0;

                // An empty tile shares the offset with the following tile, so the offset of
                // the following index entry (or the size of the sub-file) ends the tile data
                long nextTileOffset;
                if (column + 1 < columns)
                    nextTileOffset = GetTileOffset(entries.AsSpan((column + 1) * IndexEntrySize, IndexEntrySize));
                else if (rowEndsTheTileGrid)
                    nextTileOffset = subFile.Size;
                else
                    nextTileOffset = GetTileOffset(entries.AsSpan(columns * IndexEntrySize, IndexEntrySize));

                var tileLength = (int)(nextTileOffset - GetTileOffset(entry));
                if (tileLength < 0)
                    throw new InvalidDataException($"The tile index of the mapsforge file '{Path}' is corrupt");

                var tileData = tileLength > 0
                    ? await ReadBytesAsync(subFile.StartOffset + GetTileOffset(entry), tileLength).ConfigureAwait(false)
                    : [];

                coveredTiles[row * columns + column] = (isWaterTile, tileData);
                allWater &= isWaterTile;
                hasData |= tileLength > 0;
            }
        }

        if (allWater)
        {
            // Meta flags only: a converter renders the tile as water, there is no tile data
            return [TileMetaFlagWater];
        }

        if (!hasData && !coveredTiles.Any(t => t.IsWaterTile))
            // Empty tile: no data
            return null;

        var envelope = BuildTileEnvelope(subFile, firstTileX, firstTileY, columns, coveredTiles);
        _envelopeCache.Add(tile.Id, envelope);
        return envelope;
    }

    /// <summary>
    /// Small thread-safe LRU cache for the envelopes of the last requested tiles; the envelopes
    /// are immutable, so callers may share the same byte[] instance.
    /// </summary>
    private sealed class EnvelopeCache(int capacity)
    {
        private readonly object _lock = new();
        private readonly int _capacity = capacity;
        private readonly Dictionary<ulong, LinkedListNode<(ulong Key, byte[] Envelope)>> _entries = [];
        private readonly LinkedList<(ulong Key, byte[] Envelope)> _order = [];

        public byte[]? Get(ulong key)
        {
            lock (_lock)
            {
                if (!_entries.TryGetValue(key, out var node))
                    return null;

                _order.Remove(node);
                _order.AddFirst(node);
                return node.Value.Envelope;
            }
        }

        public void Add(ulong key, byte[] envelope)
        {
            lock (_lock)
            {
                if (_entries.TryGetValue(key, out var existing))
                {
                    existing.Value = (key, envelope);
                    _order.Remove(existing);
                    _order.AddFirst(existing);
                    return;
                }

                var node = new LinkedListNode<(ulong Key, byte[] Envelope)>((key, envelope));
                _order.AddFirst(node);
                _entries[key] = node;

                if (_entries.Count > _capacity)
                {
                    var oldest = _order.Last!;
                    _order.RemoveLast();
                    _entries.Remove(oldest.Value.Key);
                }
            }
        }
    }

    /// <summary>
    /// Builds the envelope with the tile data of the covered base zoom tiles of the requested
    /// tile, see the class remarks for the layout.
    /// </summary>
    private byte[] BuildTileEnvelope(MapsforgeSubFile subFile, int firstTileX, int firstTileY, int columns,
        (bool IsWaterTile, byte[] Data)[] coveredTiles)
    {
        using var envelope = new MemoryStream();
        envelope.WriteByte(0);
        WriteVbeu(envelope, subFile.BaseZoom);
        WriteVbeu(envelope, subFile.MinZoom);
        WriteVbeu(envelope, subFile.MaxZoom);
        WriteVbeu(envelope, firstTileX);
        WriteVbeu(envelope, firstTileY);
        WriteVbeu(envelope, columns);
        WriteVbeu(envelope, coveredTiles.Length);

        WriteStringTable(envelope, PoiTags);
        WriteStringTable(envelope, WayTags);

        foreach (var (isWaterTile, tileData) in coveredTiles)
        {
            envelope.WriteByte(isWaterTile ? TileMetaFlagWater : (byte)0);
            WriteVbeu(envelope, tileData.Length);
            envelope.Write(tileData);
        }

        return envelope.ToArray();
    }

    /// <summary>
    /// Releases the open file handle of the map file.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _fileHandle.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Removes the 'mapsforge://' or 'mapsforge:' scheme prefix from a source and URL decodes it,
    /// leaving a usable file path. 'mapsforge://' addresses a path relative to the working
    /// directory, 'mapsforge:/' an absolute path.
    /// </summary>
    /// <param name="source">Source as URI string or path</param>
    /// <returns>Path of the map file</returns>
    /// <exception cref="ArgumentException">Thrown when the source contains no path</exception>
    private static string StripScheme(string source)
    {
        source = Uri.UnescapeDataString(source).Trim();

        if (source.StartsWith(MapsforgeSchemePrefix, StringComparison.OrdinalIgnoreCase))
            source = source[MapsforgeSchemePrefix.Length..];

        // Following the scheme 'mapsforge://' addresses a relative path like in an URL,
        // 'mapsforge:/' an absolute path
        if (source.StartsWith("://", StringComparison.Ordinal))
            source = source[3..];
        else if (source.StartsWith("//", StringComparison.Ordinal))
            source = source[2..];

        if (string.IsNullOrWhiteSpace(source))
            throw new ArgumentException($"The source '{source}' does not contain a path to a mapsforge map file");

        return source;
    }

    /// <summary>
    /// Read and parse the file header of the map file: magic bytes, file metadata, optional
    /// header fields, the POI and way tag string tables and the zoom interval configurations.
    /// </summary>
    private void ParseFileHeader()
    {
        Name = System.IO.Path.GetFileName(Path);

        var magicBytes = ReadBytesInternal(0, MagicByteLength);
        if (!Encoding.ASCII.GetString(magicBytes).StartsWith(MagicBytes, StringComparison.Ordinal))
            throw new InvalidDataException($"'{Path}' is not a Mapsforge map file (invalid magic bytes)");

        // The header size counts all header bytes behind the magic bytes, including the
        // header size field itself
        var headerSizeBuffer = ReadBytesInternal(MagicByteLength, sizeof(int));
        var headerSize = BinaryPrimitives.ReadInt32BigEndian(headerSizeBuffer);
        var header = ReadBytesInternal(0, MagicByteLength + sizeof(int) + headerSize);

        var pos = MagicByteLength + sizeof(int); // the header size field is followed by the file version
        FileVersion = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(pos));
        pos += sizeof(int);
        if (FileVersion is < 3 or > 5)
            throw new NotSupportedException($"Mapsforge map file version {FileVersion} is not supported, only versions 3 to 5 are supported");
        pos += sizeof(long); // file size, not needed
        DateOfCreation = DateTime.UnixEpoch.AddMilliseconds(BinaryPrimitives.ReadInt64BigEndian(header.AsSpan(pos)));
        pos += sizeof(long);
        MinLatitude = ToDegrees(BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(pos)));
        MinLongitude = ToDegrees(BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(pos + 4)));
        MaxLatitude = ToDegrees(BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(pos + 8)));
        MaxLongitude = ToDegrees(BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(pos + 12)));
        pos += 16;
        TileSize = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(pos));
        pos += sizeof(ushort);
        Projection = ReadString(header, ref pos);
        if (!Projection.Contains("Mercator", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException($"Projection '{Projection}' of the mapsforge file is not supported, only 'MercatorProjection' is supported");
        var flags = header[pos];
        pos += 1;
        IsDebugInfoAvailable = (flags & 0x80) != 0;

        if ((flags & 0x40) != 0)
        {
            StartPositionLatitude = ToDegrees(BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(pos)));
            StartPositionLongitude = ToDegrees(BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(pos + 4)));
            pos += 8;
        }

        if ((flags & 0x20) != 0)
        {
            StartZoom = header[pos];
            pos += 1;
        }

        if ((flags & 0x10) != 0)
            LanguagePreference = ReadString(header, ref pos);

        if ((flags & 0x08) != 0)
            Comment = ReadString(header, ref pos);

        if ((flags & 0x04) != 0)
            CreatedBy = ReadString(header, ref pos);

        var poiTagCount = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(pos));
        pos += sizeof(ushort);
        PoiTags = ReadStrings(header, ref pos, poiTagCount);

        var wayTagCount = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(pos));
        pos += sizeof(ushort);
        WayTags = ReadStrings(header, ref pos, wayTagCount);

        var zoomIntervalCount = header[pos];
        pos += 1;

        for (var i = 0; i < zoomIntervalCount; i++)
        {
            var baseZoom = header[pos];       // base zoom level
            var minZoom = header[pos + 1];    // minimal zoom level
            var maxZoom = header[pos + 2];    // maximal zoom level
            if (baseZoom < minZoom || baseZoom > maxZoom)
                throw new InvalidDataException(
                    $"The mapsforge file '{Path}' is corrupt: the base zoom level {baseZoom} of zoom interval {i + 1} " +
                    $"lies outside of its zoom range {minZoom}-{maxZoom}");

            _subFiles.Add(new MapsforgeSubFile(baseZoom, minZoom, maxZoom)
            {
                StartOffset = BinaryPrimitives.ReadInt64BigEndian(header.AsSpan(pos + 3)),
                Size = BinaryPrimitives.ReadInt64BigEndian(header.AsSpan(pos + 11)),
            });

            pos += ZoomIntervalConfigSize;
        }
    }

    /// <summary>
    /// Computes the tile grid of a sub-file: the grid covers the bounding box of the map file
    /// at the base zoom level of the sub-file; the tile index entries are stored row-wise
    /// starting with the top-left tile of the grid.
    /// </summary>
    /// <param name="subFile">Sub-file to compute the tile grid for</param>
    /// <returns>First tile X and Y number of the grid and its size in tiles</returns>
    private (int FirstTileX, int FirstTileY, int Columns, int Rows) GetTileGridRect(MapsforgeSubFile subFile)
    {
        var firstTileX = Math.Max(0, TileX(MinLongitude, subFile.BaseZoom));
        var firstTileY = Math.Max(0, TileY(MaxLatitude, subFile.BaseZoom));

        return (
            firstTileX,
            firstTileY,
            Math.Min((1 << subFile.BaseZoom) - 1, TileX(MaxLongitude, subFile.BaseZoom)) - firstTileX + 1,
            Math.Min((1 << subFile.BaseZoom) - 1, TileY(MinLatitude, subFile.BaseZoom)) - firstTileY + 1
        );
    }

    /// <summary>
    /// Extracts the 39-bit tile offset from a tile index entry. The top bit of the entry marks
    /// a water tile, the lower 39 bits hold the offset of the tile within the sub-file
    /// (big endian order).
    /// </summary>
    /// <param name="entry">Tile index entry bytes</param>
    /// <returns>Offset of the tile within the sub-file in bytes</returns>
    private static long GetTileOffset(ReadOnlySpan<byte> entry)
    {
        return ((long)(entry[0] & 0x7f) << 32) |
            ((long)entry[1] << 24) |
            ((long)entry[2] << 16) |
            ((long)entry[3] << 8) |
            (long)entry[4];
    }

    /// <summary>
    /// Read count bytes from the open map file at the given absolute position.
    /// </summary>
    private byte[] ReadBytesInternal(long position, int count)
    {
        var buffer = new byte[count];
        var read = 0;
        while (read < count)
        {
            var bytesRead = RandomAccess.Read(_fileHandle, buffer.AsSpan(read), position + read);
            if (bytesRead <= 0)
                throw new InvalidDataException($"Unexpected end of file while reading '{Path}' at position {position + read}");
            read += bytesRead;
        }

        return buffer;
    }

    /// <summary>
    /// Read count bytes from the open map file at the given absolute position asynchronously.
    /// </summary>
    private async Task<byte[]> ReadBytesAsync(long position, int count)
    {
        var buffer = new byte[count];
        var read = 0;
        while (read < count)
        {
            var bytesRead = await RandomAccess.ReadAsync(_fileHandle, buffer.AsMemory(read), position + read).ConfigureAwait(false);
            if (bytesRead <= 0)
                throw new InvalidDataException($"Unexpected end of file while reading '{Path}' at position {position + read}");
            read += bytesRead;
        }

        return buffer;
    }

    /// <summary>
    /// Read a string in the Mapsforge string encoding (VBE-U length followed by UTF-8 bytes).
    /// </summary>
    /// <param name="data">Byte array with the string</param>
    /// <param name="pos">Position of the string in the byte array; points to the next field after the call</param>
    /// <returns>Decoded string</returns>
    private static string ReadString(byte[] data, ref int pos)
    {
        var length = (int)ReadVbeu(data, ref pos);
        var value = Encoding.UTF8.GetString(data, pos, length);
        pos += length;
        return value;
    }

    /// <summary>
    /// Read the given amount of strings starting at the current position.
    /// </summary>
    /// <param name="data">Byte array with the strings</param>
    /// <param name="pos">Position of the first string in the byte array; points to the next field after the call</param>
    /// <param name="count">Number of strings to read</param>
    /// <returns>Decoded strings</returns>
    private static string[] ReadStrings(byte[] data, ref int pos, int count)
    {
        var result = new string[count];
        for (var i = 0; i < count; i++)
            result[i] = ReadString(data, ref pos);

        return result;
    }

    /// <summary>
    /// Read an unsigned VBE-U INT at the current position: the first bit of each byte is the
    /// continuation bit, each byte holds seven bits of the value, starting with the least
    /// significant ones.
    /// </summary>
    /// <param name="data">Byte array with the value</param>
    /// <param name="pos">Position of the value in the byte array; points to the next field after the call</param>
    /// <returns>Decoded unsigned value</returns>
    private static long ReadVbeu(byte[] data, ref int pos)
    {
        long value = 0;
        var shift = 0;
        byte currentByte;
        do
        {
            if (pos >= data.Length)
                throw new InvalidDataException("Unexpected end of data while reading VBE-U INT");
            currentByte = data[pos++];
            value |= (long)(currentByte & 0x7f) << shift;
            shift += 7;
        }
        while ((currentByte & 0x80) != 0);

        return value;
    }

    /// <summary>
    /// Write an unsigned VBE-U INT to a stream.
    /// </summary>
    /// <param name="stream">Stream to write to</param>
    /// <param name="value">Unsigned value to write</param>
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
    /// Write a string table to a stream: a VBE-U INT with the number of strings, followed by
    /// each string as VBE-U INT length + UTF-8 bytes.
    /// </summary>
    /// <param name="stream">Stream to write to</param>
    /// <param name="strings">Strings of the table</param>
    private static void WriteStringTable(Stream stream, IReadOnlyList<string> strings)
    {
        WriteVbeu(stream, strings.Count);
        foreach (var value in strings)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            WriteVbeu(stream, bytes.Length);
            stream.Write(bytes);
        }
    }

    /// <summary>
    /// Convert microdegrees to degrees.
    /// </summary>
    /// <param name="microdegrees">Geo coordinate in microdegrees</param>
    /// <returns>Geo coordinate in degrees</returns>
    private static double ToDegrees(int microdegrees)
    {
        return microdegrees / 1_000_000.0;
    }

    /// <summary>
    /// X tile number in the XYZ scheme for a longitude in degrees and a zoom level.
    /// </summary>
    /// <param name="longitude">Longitude in degrees</param>
    /// <param name="zoom">Zoom level</param>
    /// <returns>X tile number</returns>
    private static int TileX(double longitude, int zoom)
    {
        return (int)Math.Floor((longitude + 180.0) / 360.0 * Math.Pow(2, zoom));
    }

    /// <summary>
    /// Y tile number in the XYZ scheme for a latitude in degrees and a zoom level.
    /// </summary>
    /// <param name="latitude">Latitude in degrees</param>
    /// <param name="zoom">Zoom level</param>
    /// <returns>Y tile number</returns>
    private static int TileY(double latitude, int zoom)
    {
        var radians = latitude * Math.PI / 180.0;
        return (int)Math.Floor((1.0 - Math.Log(Math.Tan(radians) + 1.0 / Math.Cos(radians)) / Math.PI) / 2.0 * Math.Pow(2, zoom));
    }
}
