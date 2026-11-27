using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;
using VecTiles.Common.Enums;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;

namespace VecTiles.DataSources.PmTiles;

/// <summary>
/// An <see cref="ITileDataSource"/> implementation for PMTiles archives
/// (see https://github.com/protomaps/PMTiles/blob/main/spec/v3/spec.md).
/// </summary>
/// <remarks>
/// A PMTiles archive bundles all tiles of a tileset in a single file, addressed by Hilbert
/// curve tile ids and organized in a root directory with optional leaf directories. This data
/// source reads the header, the JSON metadata and the root directory when it is created and
/// fetches single tile byte ranges on demand — from a file via positioned reads or from an
/// HTTP(S) server via range requests. Leaf directories are kept in a small cache, the root
/// directory stays in memory; tiles are decompressed on the fly according to the tile
/// compression of the header and passed through to an <see cref="ITileConverter"/> unchanged.
/// </remarks>
public class PmTilesTileDataSource : ITileDataSource, IDisposable
{
    private const string SchemePrefix = "pmtiles://";
    private const int LeafDirectoryCacheSize = 4;

    private readonly IPmTilesRangeReader _rangeReader;
    private readonly PmTilesEntry[] _rootEntries;
    private readonly PmTilesCompression _internalCompression;
    private readonly PmTilesCompression _tileCompression;
    private readonly ulong _tileDataOffset;
    private readonly ulong _leafDirectoriesOffset;
    private readonly object _leafDirectoryCacheLock = new();
    private readonly Dictionary<ulong, PmTilesEntry[]> _leafDirectoryCache = new();
    private bool _disposed;

    ///  <summary>
    ///  Initializes a new instance of the <see cref="PmTilesTileDataSource"/> class for the given source.
    ///  </summary>
    ///  <param name="source">The PMTiles archive as URL: 'pmtiles://file://path/to/archive.pmtiles' for a file,
    ///  'pmtiles://https://host/path/archive.pmtiles' or 'pmtiles://http://host/path/archive.pmtiles' for an
    ///  HTTP(S) server that supports range requests. The 'pmtiles://' prefix and the 'file://' prefix are
    ///  optional, a plain file path is also accepted.</param>
    ///  <param name="minZoom">Minimum zoom of the source, used to restrict the zoom range of the archive</param>
    ///  <param name="maxZoom">Maximum zoom of the source, used to restrict the zoom range of the archive</param>
    ///  <exception cref="ArgumentException">Thrown when no source is given or the zoom range does not intersect the zoom range of the archive</exception>
    ///  <exception cref="FileNotFoundException">Thrown when the archive file does not exist</exception>
    ///  <exception cref="InvalidDataException">Thrown when the file is not a PMTiles archive or it is corrupt</exception>
    ///  <exception cref="NotSupportedException">Thrown when the compression of the archive is not supported</exception>
    public PmTilesTileDataSource(string source, int minZoom = 0, int maxZoom = 22)
    {
        if (string.IsNullOrWhiteSpace(source))
            throw new ArgumentException("A pmtiles source must be provided", nameof(source));

        Source = StripScheme(source);
        _rangeReader = CreateRangeReader(Source);

        try
        {
            var headerBytes = _rangeReader.ReadAsync(0, PmTilesHeader.Length).GetAwaiter().GetResult();
            Header = PmTilesHeader.Parse(headerBytes);

            _internalCompression = Header.InternalCompression;
            _tileCompression = Header.TileCompression;
            _tileDataOffset = Header.TileDataOffset;
            _leafDirectoriesOffset = Header.LeafDirectoriesOffset;

            var metadata = ReadMetadata();
            Name = metadata?.Name ?? string.Empty;
            Attribution = metadata?.Attribution ?? string.Empty;

            SourceType = ReadSourceType(metadata);
            MinZoom = Header.MinZoom;
            MaxZoom = Header.MaxZoom > 0 ? Header.MaxZoom : (metadata?.MaxZoom ?? 0);

            // Restrict the zoom range of the archive by the zoom range given in the parameters.
            // Above the archive's maximum zoom level the tile data of the highest zoom level is
            // overzoomed by the calling side, below the archive's minimum zoom level no data exists.
            MinZoom = Math.Max(minZoom, MinZoom);
            MaxZoom = Math.Min(maxZoom, MaxZoom);

            if (MinZoom > MaxZoom)
                throw new ArgumentException(
                    $"The zoom range {minZoom}-{maxZoom} passed to the data source does not intersect " +
                    $"the zoom range {Header.MinZoom}-{Header.MaxZoom} of the pmtiles archive");

            (Center, CenterZoom) = ReadCenter(metadata);

            var rootDirectory = _rangeReader.ReadAsync((long)Header.RootDirectoryOffset, checked((int)Header.RootDirectoryLength))
                .GetAwaiter().GetResult();
            _rootEntries = PmTilesDirectory.Deserialize(Decompress(rootDirectory, _internalCompression));
        }
        catch
        {
            _rangeReader.Dispose();
            throw;
        }
    }

    /// <summary>
    /// The source URL this data source was created for, without the 'pmtiles://' prefix.
    /// </summary>
    public string Source { get; }

    /// <summary>
    /// The parsed header of the archive.
    /// </summary>
    public PmTilesHeader Header { get; }

    /// <inheritdoc/>
    public string Name { get; }

    /// <inheritdoc/>
    public string Attribution { get; }

    ///  <summary>
    ///  Minimum zoom level of the source; restricted by the zoom range given to the constructor.
    ///  </summary>
    public int MinZoom { get; }

    ///  <summary>
    ///  Maximum zoom level of the source; restricted by the zoom range given to the constructor.
    ///  </summary>
    public int MaxZoom { get; }

    ///  <summary>
    ///  Vector data for MVT and MapLibre vector tiles, raster data for image tiles and vector
    ///  data for archives of unknown tile type that declare vector layers in their metadata.
    ///  </summary>
    public SourceType SourceType { get; }

    ///  <summary>
    ///  PMTiles tile coordinates follow the XYZ scheme (tile origin at top-left).
    ///  </summary>
    public Scheme Scheme { get; } = Scheme.Xyz;

    ///  <summary>
    ///  Center of the archive as longitude/latitude, read from the header or the JSON metadata
    ///  of the archive. Empty if neither provides a usable center.
    ///  </summary>
    public float[] Center { get; } = [];

    ///  <summary>
    ///  Zoom level of the center of the archive, read from the header or the JSON metadata of
    ///  the archive, null if neither provides a usable value.
    ///  </summary>
    public float? CenterZoom { get; }

    /// <summary>
    /// Get binary data for a tile from the archive.
    /// </summary>
    /// <param name="tile">Tile to get binary data for</param>
    /// <returns>Task which returns the binary data of the tile, or null when the archive does not contain the tile</returns>
    public async Task<byte[]?> GetTileAsync(Tile tile)
    {
        if (tile.Zoom is < 0 or > TileId.MaxZoom || tile.X >= 1L << tile.Zoom || tile.Y >= 1L << tile.Zoom)
            return null;

        var tileId = TileId.FromTileCoordinates(tile.Zoom, tile.X, tile.Y);

        var entry = PmTilesDirectory.Find(_rootEntries, tileId);

        // A run length of 0 marks a leaf directory entry: read the leaf directory, cache it and
        // continue the search there
        if (entry is { RunLength: 0 })
        {
            var leafEntries = await GetLeafDirectoryAsync(entry.Value).ConfigureAwait(false);
            entry = PmTilesDirectory.Find(leafEntries, tileId);
        }

        if (entry is not { RunLength: > 0 } tileEntry)
            return null;

        var data = await _rangeReader.ReadAsync(
                (long)(_tileDataOffset + tileEntry.Offset), tileEntry.Length)
            .ConfigureAwait(false);

        return Decompress(data, _tileCompression);
    }

    ///  <summary>
    ///  Get binary data for a non-tiled source in URL notation. Not supported for this data source.
    ///  </summary>
    ///  <param name="source">Source as URL string</param>
    ///  <returns>Task which returns binary data</returns>
    ///  <exception cref="NotImplementedException">Always thrown, because this data source does not provide this method</exception>
    public Task<byte[]?> GetBytesAsync(string source)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Releases the file or HTTP resources of the data source.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _rangeReader.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Strips the 'pmtiles://' prefix and an optional 'file://' prefix from a source URL.
    /// </summary>
    /// <param name="source">The source URL</param>
    /// <returns>The path or URL of the archive</param>
    private static string StripScheme(string source)
    {
        var stripped = source.StartsWith(SchemePrefix, StringComparison.OrdinalIgnoreCase)
            ? source[SchemePrefix.Length..]
            : source;

        return stripped.StartsWith("file://", StringComparison.OrdinalIgnoreCase)
            ? stripped["file://".Length..]
            : stripped;
    }

    /// <summary>
    /// Creates the range reader for the source: a positioned file reader for file paths and
    /// an HTTP range request reader for http(s) URLs.
    /// </summary>
    /// <param name="source">The path or URL of the archive</param>
    /// <returns>The range reader</returns>
    private static IPmTilesRangeReader CreateRangeReader(string source)
    {
        if (source.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            source.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return new HttpRangeReader(source);

        if (!File.Exists(source))
            throw new FileNotFoundException($"The pmtiles archive does not exist: '{source}'", source);

        return new FileRangeReader(source);
    }

    /// <summary>
    /// Reads and decompresses the JSON metadata of the archive, if it has any.
    /// </summary>
    /// <returns>The metadata, or null when the archive has no metadata section</returns>
    private PmTilesMetadata? ReadMetadata()
    {
        if (Header.MetadataLength == 0)
            return null;

        var data = _rangeReader.ReadAsync((long)Header.MetadataOffset, checked((int)Header.MetadataLength))
            .GetAwaiter().GetResult();

        return PmTilesMetadata.Parse(Decompress(data, _internalCompression));
    }

    /// <summary>
    /// Determines the source type from the tile type of the header and the vector layers of the metadata.
    /// </summary>
    /// <param name="metadata">The JSON metadata of the archive</param>
    /// <returns>The source type</returns>
    private SourceType ReadSourceType(PmTilesMetadata? metadata)
    {
        return Header.TileType switch
        {
            PmTilesTileType.Mvt or PmTilesTileType.MapLibreVector => SourceType.Vector,
            PmTilesTileType.Png or PmTilesTileType.Jpeg or PmTilesTileType.Webp or PmTilesTileType.Avif => SourceType.Raster,
            _ => metadata is { VectorLayerCount: > 0 } ? SourceType.Vector : SourceType.Raster
        };
    }

    /// <summary>
    /// Reads the center of the archive: first from the header, then from the JSON metadata
    /// ('center' entry), then from the midpoint of the bounds ('bounds' entry of the metadata
    /// or the header positions). An empty center and no zoom are returned when nothing is usable.
    /// </summary>
    /// <param name="metadata">The JSON metadata of the archive</param>
    /// <returns>The center as longitude/latitude and its zoom level</returns>
    private (float[] Center, float? CenterZoom) ReadCenter(PmTilesMetadata? metadata)
    {
        // The center zoom is only meaningful together with a center
        var headerCenterUsable = Header.CenterLongitude.HasValue && Header.CenterLatitude.HasValue &&
            !(Header.CenterLongitude == 0 && Header.CenterLatitude == 0);

        if (headerCenterUsable)
            return (new[] { (float)Header.CenterLongitude!.Value, (float)Header.CenterLatitude!.Value },
                Header.CenterZoom is >= 0 and <= 31 ? Header.CenterZoom : null);

        var center = metadata?.ReadCenter();
        if (center.HasValue)
            return center.Value;

        // Fall back to the midpoint of the bounds
        var bounds = metadata?.ReadBounds() ?? ReadHeaderBounds();
        if (bounds.HasValue)
            return ([(bounds.Value.minX + bounds.Value.maxX) / 2f, (bounds.Value.minY + bounds.Value.maxY) / 2f], null);

        return ([], null);
    }

    /// <summary>
    /// Reads the bounds of the header, if all of them are usable.
    /// </summary>
    /// <returns>The bounds as minX, minY, maxX, maxY in degrees, or null</returns>
    private (float minX, float minY, float maxX, float maxY)? ReadHeaderBounds()
    {
        if (!Header.MinLongitude.HasValue || !Header.MinLatitude.HasValue ||
            !Header.MaxLongitude.HasValue || !Header.MaxLatitude.HasValue)
            return null;

        return ((float)Header.MinLongitude.Value, (float)Header.MinLatitude.Value,
            (float)Header.MaxLongitude.Value, (float)Header.MaxLatitude.Value);
    }

    /// <summary>
    /// Reads, decompresses and caches a leaf directory.
    /// </summary>
    /// <param name="entry">The leaf directory entry of the root directory</param>
    /// <returns>The entries of the leaf directory</returns>
    private async Task<PmTilesEntry[]> GetLeafDirectoryAsync(PmTilesEntry entry)
    {
        lock (_leafDirectoryCacheLock)
        {
            if (_leafDirectoryCache.TryGetValue(entry.Offset, out var cached))
                return cached;
        }

        var data = await _rangeReader.ReadAsync(
                (long)(_leafDirectoriesOffset + entry.Offset), entry.Length)
            .ConfigureAwait(false);

        var entries = PmTilesDirectory.Deserialize(Decompress(data, _internalCompression));

        lock (_leafDirectoryCacheLock)
        {
            if (_leafDirectoryCache.Count >= LeafDirectoryCacheSize)
                _leafDirectoryCache.Clear();
            _leafDirectoryCache[entry.Offset] = entries;
        }

        return entries;
    }

    /// <summary>
    /// Decompresses a section of the archive according to its compression.
    /// </summary>
    /// <param name="data">The compressed bytes</param>
    /// <param name="compression">The compression of the section</param>
    /// <returns>The decompressed bytes, or the input bytes when the section is not compressed</returns>
    /// <exception cref="NotSupportedException">Thrown when the compression is not supported</exception>
    private static byte[] Decompress(byte[] data, PmTilesCompression compression)
    {
        return compression switch
        {
            PmTilesCompression.None or PmTilesCompression.Unknown => data,
            PmTilesCompression.Gzip => Decompress(data, static stream => new GZipStream(stream, CompressionMode.Decompress)),
            PmTilesCompression.Brotli => Decompress(data, static stream => new BrotliStream(stream, CompressionMode.Decompress)),
            _ => throw new NotSupportedException($"The pmtiles compression '{compression}' is not supported")
        };
    }

    /// <summary>
    /// Decompresses a byte array with a decompressor created by the given factory.
    /// </summary>
    /// <param name="data">The compressed bytes</param>
    /// <param name="createDecompressor">Factory for the decompression stream around the input</param>
    /// <returns>The decompressed bytes</returns>
    private static byte[] Decompress(byte[] data, Func<Stream, Stream> createDecompressor)
    {
        using var input = new MemoryStream(data);
        using var decompressor = createDecompressor(input);
        using var output = new MemoryStream(data.Length * 2);
        decompressor.CopyTo(output);
        return output.ToArray();
    }

    /// <summary>
    /// Reads a byte range from a PMTiles archive.
    /// </summary>
    private interface IPmTilesRangeReader : IDisposable
    {
        /// <summary>
        /// Reads the given range of bytes from the archive.
        /// </summary>
        /// <param name="offset">Offset of the range, relative to the start of the archive</param>
        /// <param name="length">Length of the range in bytes</param>
        /// <returns>The bytes of the range</returns>
        Task<byte[]> ReadAsync(long offset, int length);
    }

    /// <summary>
    /// Reads byte ranges from a PMTiles file in the file system with positioned reads, so only
    /// the requested bytes are read and held in memory.
    /// </summary>
    private sealed class FileRangeReader : IPmTilesRangeReader
    {
        private readonly SafeFileHandle _fileHandle;

        public FileRangeReader(string path)
        {
            _fileHandle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.Read, FileOptions.Asynchronous);
        }

        public async Task<byte[]> ReadAsync(long offset, int length)
        {
            var data = GC.AllocateUninitializedArray<byte>(length);
            await RandomAccess.ReadAsync(_fileHandle, data, offset).ConfigureAwait(false);
            return data;
        }

        public void Dispose()
        {
            _fileHandle.Dispose();
        }
    }

    /// <summary>
    /// Reads byte ranges from a PMTiles archive on an HTTP(S) server with range requests. The
    /// server must support range requests, which all PMTiles hosting is required to provide.
    /// </summary>
    private sealed class HttpRangeReader : IPmTilesRangeReader
    {
        // Shared client: a new HttpClient per reader exhausts sockets under load
        private static readonly HttpClient Client = new(new HttpClientHandler { AllowAutoRedirect = true })
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        private readonly Uri _uri;

        public HttpRangeReader(string url)
        {
            _uri = new Uri(url);
        }

        public async Task<byte[]> ReadAsync(long offset, int length)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, _uri);
            request.Headers.Range = new RangeHeaderValue(offset, offset + length - 1);

            using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            if (response.StatusCode != HttpStatusCode.PartialContent)
                throw new InvalidDataException($"The server of '{_uri}' does not support range requests");

            var data = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            if (data.Length != length)
                throw new InvalidDataException(
                    $"The server of '{_uri}' returned {data.Length} bytes instead of the requested {length}");

            return data;
        }

        public void Dispose()
        {
            // The shared HttpClient is not disposed
        }
    }

    /// <summary>
    /// The JSON metadata of a PMTiles archive
    /// (see https://github.com/protomaps/PMTiles/blob/main/spec/v3/spec.md, chapter 5).
    /// Writers add more keys, e.g. TileJSON keys like bounds, center, minzoom and maxzoom.
    /// </summary>
    private sealed class PmTilesMetadata
    {
        public string? Name { get; private init; }
        public string? Attribution { get; private init; }
        public int? MaxZoom { get; private init; }
        public int VectorLayerCount { get; private init; }

        private byte[] Json { get; init; } = [];

        /// <summary>
        /// Parses the metadata from its JSON bytes.
        /// </summary>
        /// <param name="json">The decompressed JSON bytes</param>
        /// <returns>The parsed metadata</returns>
        public static PmTilesMetadata Parse(byte[] json)
        {
            string? name = null;
            string? attribution = null;
            int? maxZoom = null;
            var vectorLayerCount = 0;

            try
            {
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;

                if (root.ValueKind == JsonValueKind.Object)
                {
                    foreach (var property in root.EnumerateObject())
                    {
                        switch (property.Name)
                        {
                            case "name" when property.Value.ValueKind == JsonValueKind.String:
                                name = property.Value.GetString();
                                break;
                            case "attribution" when property.Value.ValueKind == JsonValueKind.String:
                                attribution = property.Value.GetString();
                                break;
                            case "maxzoom" when property.Value.ValueKind == JsonValueKind.Number:
                                maxZoom = property.Value.GetInt32();
                                break;
                            case "vector_layers" when property.Value.ValueKind == JsonValueKind.Array:
                                vectorLayerCount = property.Value.GetArrayLength();
                                break;
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // Corrupt metadata: keep what was read so far
            }

            return new PmTilesMetadata
            {
                Name = name,
                Attribution = attribution,
                MaxZoom = maxZoom,
                VectorLayerCount = vectorLayerCount,
                Json = json
            };
        }

        /// <summary>
        /// Reads the 'center' entry of the metadata: an array or a 'lon,lat[,zoom]' string.
        /// </summary>
        /// <returns>The center as longitude/latitude and its zoom level, or null when the entry is missing or unusable</returns>
        public (float[] Center, float? CenterZoom)? ReadCenter()
        {
            try
            {
                using var document = JsonDocument.Parse(Json);
                if (!document.RootElement.TryGetProperty("center", out var center))
                    return null;

                float[]? coordinates = null;
                float? zoom = null;

                if (center.ValueKind == JsonValueKind.Array && center.GetArrayLength() >= 2)
                {
                    coordinates = center.EnumerateArray()
                        .Select(element => element.GetSingle())
                        .Take(3)
                        .ToArray();
                    if (coordinates.Length >= 3)
                        zoom = coordinates[2];
                }
                else if (center.ValueKind == JsonValueKind.String)
                {
                    var components = center.GetString()?.Split(',');
                    if (components is { Length: >= 2 })
                    {
                        coordinates = components
                            .Select(component => float.TryParse(component.Trim(), System.Globalization.NumberStyles.Float,
                                System.Globalization.NumberFormatInfo.InvariantInfo, out var value) ? value : (float?)null)
                            .Take(3)
                            .OfType<float>()
                            .ToArray();
                        if (components.Length >= 3)
                            zoom = coordinates.Length >= 3 ? coordinates[2] : null;
                    }
                }

                if (coordinates is { Length: >= 2 })
                    return ([coordinates[0], coordinates[1]], zoom);
            }
            catch (JsonException)
            {
                // Corrupt metadata: no center
            }

            return null;
        }

        /// <summary>
        /// Reads the 'bounds' entry of the metadata: an array or a 'minX,minY,maxX,maxY' string.
        /// </summary>
        /// <returns>The bounds as minX, minY, maxX, maxY in degrees, or null when the entry is missing or unusable</returns>
        public (float minX, float minY, float maxX, float maxY)? ReadBounds()
        {
            try
            {
                using var document = JsonDocument.Parse(Json);
                if (!document.RootElement.TryGetProperty("bounds", out var bounds))
                    return null;

                float[]? values = null;

                if (bounds.ValueKind == JsonValueKind.Array && bounds.GetArrayLength() == 4)
                {
                    values = bounds.EnumerateArray()
                        .Select(element => element.GetSingle())
                        .ToArray();
                }
                else if (bounds.ValueKind == JsonValueKind.String)
                {
                    values = bounds.GetString()?.Split(',')
                        .Select(component => float.TryParse(component.Trim(), System.Globalization.NumberStyles.Float,
                            System.Globalization.NumberFormatInfo.InvariantInfo, out var value) ? value : (float?)null)
                        .OfType<float>()
                        .Take(4)
                        .ToArray();
                }

                if (values is { Length: 4 })
                    return (values[0], values[1], values[2], values[3]);
            }
            catch (JsonException)
            {
                // Corrupt metadata: no bounds
            }

            return null;
        }
    }
}
