using System.Text;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;

namespace VecTiles.Converters.Mapsforge;

/// <summary>
/// Converts the tile envelopes of a Mapsforge tile data source (see
/// VecTiles.DataSources.Mapsforge.MapsforgeTileDataSource) to a vector tile.
/// </summary>
/// <remarks>
/// The envelope carries the raw Mapsforge tile data of all base zoom tiles covered by the
/// requested tile (Mapsforge stores all data of a zoom interval in the tile grid of the intervals
/// base zoom level and the data source serves it at the minimum zoom level of the interval).
/// This converter decodes the tile data (zoom table, POIs and ways with delta encoded coordinates
/// in microdegrees) and transforms the coordinates to the normalized 512x512 tile space of the
/// requested tile, so the result is equivalent to a natively rendered vector tile of the requested
/// tile (overzoom included).
///
/// The tile payload layout follows the Mapsforge binary map file specification,
/// https://github.com/mapsforge/mapsforge/blob/master/docs/Specification-Binary-Map-File.md.
/// The source layer and the tags of the resulting features follow from the raw Mapsforge tags and
/// are determined by an <see cref="ITagsConverter"/>; by default the
/// <see cref="MapsforgeTagsConverter"/> is used, which implements the OpenMapTiles schema.
/// </remarks>
public class MapsforgeTileConverter : ITileConverter
{
    /// <summary>
    /// Meta flag of a tile envelope: the tile is completely covered by water and has no tile data.
    /// </summary>
    private const byte EnvelopeMetaFlagWater = 0x01;

    /// <summary>
    /// Meta flag of a base zoom tile inside an envelope: the tile is completely water.
    /// </summary>
    private const byte TileMetaFlagWater = 0x01;

    private const int DebugSignatureLength = 32;

    /// <summary>
    /// Buffer in tile pixels around the requested tile used when clipping the decoded features
    /// to the requested tile. The buffer keeps stroke ends of lines crossing the tile border
    /// intact and corresponds to the convention of vector tiles, where geometries may extend
    /// slightly beyond the tile bounds.
    /// </summary>
    private const double ClipBuffer = 4.0;

    /// <summary>
    /// Clip rectangle around the requested tile in the normalized 512x512 tile space; decoded
    /// features outside of it are dropped, crossing geometries are clipped to it. Clipping keeps
    /// the features of the converted tile bounded by the tile area, which is essential for the
    /// overzoom tiles: without it, every tile generated from a base zoom tile of a whole zoom
    /// interval would carry the full data of all covered base tiles.
    /// </summary>
    private static readonly Envelope ClipRectangle =
        new(-ClipBuffer, 512.0 + ClipBuffer, -ClipBuffer, 512.0 + ClipBuffer);


    private static readonly GeometryFactory GeometryFactory = GeometryFactory.Default;

    /// <summary>
    /// Optional converter for the tags and source layers of the features to the conventions of
    /// the style rendering the converted tile. When null, the <see cref="MapsforgeTagsConverter"/>
    /// with the OpenMapTiles schema is used.
    /// </summary>
    private readonly ITagsConverter? _tagsConverter;

    /// <summary>
    /// Initializes a new instance of the <see cref="MapsforgeTileConverter"/> class.
    /// </summary>
    /// <param name="tagsConverter">Optional tags and source layer converter; null uses the
    /// <see cref="MapsforgeTagsConverter"/> with the OpenMapTiles schema.</param>
    public MapsforgeTileConverter(ITagsConverter? tagsConverter = null)
    {
        _tagsConverter = tagsConverter;
    }

    /// <inheritdoc/>
    public Task<VectorTile?> Convert(Tile requestedTile, Tile providedTile, byte[] data)
    {
        var vectorTile = new VectorTile
        {
            TileId = requestedTile.Id,
        };

        if (data is null || data.Length == 0)
        {
            return Task.FromResult<VectorTile?>(vectorTile);
        }

        var pos = 0;
        if ((data[pos++] & EnvelopeMetaFlagWater) != 0)
        {
            // The tile is completely covered by water: no tile data exists, the full tile
            // polygon makes the renderer draw the water; the raw tags follow the convention of
            // a mapped water area, so the tags converters handle it like any other water area
            var attributes = new AttributesTable();
            attributes.Add("natural", "water");
            attributes.Add("class", "lake");

            var waterLayers = new Dictionary<string, Layer>(StringComparer.Ordinal);
            AddConvertedFeature(waterLayers, _tagsConverter, GeometryFactory.ToGeometry(new Envelope(0, 512, 0, 512)),
                "water", attributes);

            foreach (var layer in waterLayers.Values)
            {
                vectorTile.Layers.Add(layer);
            }

            return Task.FromResult<VectorTile?>(vectorTile);
        }

        // Envelope header, see the remarks of the mapsforge tile data source
        var baseZoom = (int)ReadVbeu(data, ref pos);
        var subFileMinZoom = (int)ReadVbeu(data, ref pos);
        var subFileMaxZoom = (int)ReadVbeu(data, ref pos);
        var firstTileX = (int)ReadVbeu(data, ref pos);
        var firstTileY = (int)ReadVbeu(data, ref pos);
        var columns = (int)ReadVbeu(data, ref pos);
        var tileCount = (int)ReadVbeu(data, ref pos);

        var poiTags = ReadStringTable(data, ref pos);
        var wayTags = ReadStringTable(data, ref pos);

        // The number of POIs and ways to decode follows from the row of the per-tile zoom table
        // of the requested zoom level; the rows are stored for the zoom range of the sub-file
        var zoomTableRow = Math.Clamp(requestedTile.Zoom, subFileMinZoom, subFileMaxZoom) - subFileMinZoom;
        var zoomTableRowCount = subFileMaxZoom - subFileMinZoom + 1;

        // Features are collected in layers by the converted source layer
        var layers = new Dictionary<string, Layer>(StringComparer.Ordinal);

        for (var tileIndex = 0; tileIndex < tileCount && pos < data.Length; tileIndex++)
        {
            var tileFlags = data[pos++];
            var tileLength = (int)ReadVbeu(data, ref pos);

            var baseTileX = firstTileX + tileIndex % columns;
            var baseTileY = firstTileY + tileIndex / columns;

            // Overzoom tiles see only a fraction of the base zoom tiles of the envelope; skip
            // the base tiles that do not intersect the requested tile (in the tile space of the
            // requested tile) to avoid decoding and carrying their full data. The skip must
            // still advance over the tile_length bytes: the gaps would desynchronize the whole
            // tile walk.
            if (BaseTileIntersectsRequestedTile(baseTileX, baseTileY, baseZoom, requestedTile) &&
                (tileFlags & TileMetaFlagWater) == 0 && tileLength > 0 && pos + tileLength <= data.Length)
            {
                DecodeTile(data, pos, tileLength, requestedTile, baseZoom,
                    baseTileX, baseTileY, zoomTableRow, zoomTableRowCount, poiTags, wayTags, layers);
            }

            pos += tileLength;
        }

        foreach (var layer in layers.Values)
        {
            vectorTile.Layers.Add(layer);
        }

        return Task.FromResult<VectorTile?>(vectorTile);
    }

    /// <summary>
    /// Decodes the tile data of a single base zoom tile: tile header (zoom table and first way
    /// offset), POIs and ways. The raw features are collected in the given layers.
    /// </summary>
    /// <param name="data">Byte array with the tile data at the given position and length</param>
    /// <param name="requestedTile">Tile requested by the renderer; the target of the coordinate transformation</param>
    /// <param name="baseZoom">Base zoom level of the sub-file; the tile grid of the tile data</param>
    /// <param name="baseTileX">X tile number of the tile in the base zoom tile grid</param>
    /// <param name="baseTileY">Y tile number of the tile in the base zoom tile grid</param>
    /// <param name="zoomTableRow">Row of the zoom table to decode, 0 = minimum zoom of the sub-file</param>
    /// <param name="zoomTableRowCount">Number of rows of the zoom table</param>
    /// <param name="poiTags">POI tag string table of the file header</param>
    /// <param name="wayTags">Way tag string table of the file header</param>
    /// <param name="layers">Layers to add the decoded features to</param>
    private void DecodeTile(byte[] data, int position, int length, Tile requestedTile, int baseZoom,
        int baseTileX, int baseTileY, int zoomTableRow, int zoomTableRowCount,
        string[] poiTags, string[] wayTags, Dictionary<string, Layer> layers)
    {
        var pos = position;
        var end = position + length;

        // Debug signature of the tile data; when the file contains debug information, all tile
        // records (tile header, POIs, ways) carry their 32-byte signature
        var hasDebugSignatures = data[pos] == '#';
        if (hasDebugSignatures)
            pos += DebugSignatureLength;

        // Zoom table: one row of POI and way counts per zoom level of the sub-file; like the
        // mapsforge reference, the row values are per zoom level and are summed up cumulatively
        // while reading; the row of the requested zoom level holds the POIs and ways to decode
        long poiCount = 0, wayCount = 0, cumulativePois = 0, cumulativeWays = 0;
        for (var row = 0; row < zoomTableRowCount; row++)
        {
            cumulativePois += ReadVbeu(data, ref pos);
            cumulativeWays += ReadVbeu(data, ref pos);
            if (row == zoomTableRow)
            {
                poiCount = cumulativePois;
                wayCount = cumulativeWays;
            }
        }

        // The offset of the first way counts from the position behind this field
        var firstWayOffset = (int)ReadVbeu(data, ref pos);
        var waysPos = pos + firstWayOffset;

        // Tile coordinate origin: top-left corner of the tile in microdegrees; the coordinates
        // of the tile data are relative to this corner
        var tileLatMicro = (long)Math.Round(TileYToLatitude(baseTileY, baseZoom) * 1_000_000.0);
        var tileLonMicro = (long)Math.Round(TileXToLongitude(baseTileX, baseZoom) * 1_000_000.0);

        // Decode the POIs up to the count of the zoom table row
        for (long poi = 0; poi < poiCount && pos < end; poi++)
        {
            if (hasDebugSignatures)
                // Debug signature of the POI
                pos += DebugSignatureLength;

            var latMicro = tileLatMicro + ReadVbeS(data, ref pos);
            var lonMicro = tileLonMicro + ReadVbeS(data, ref pos);

            var specialByte = data[pos++];
            var tagCount = specialByte & 0x0f;

            // First pass: skip through the tags and the flag fields, noting where they start;
            // the tags are decoded only for POIs inside the requested tile below
            var tagsPos = pos;
            var namePos = -1;
            var housenumberPos = -1;
            var elevationPos = -1;
            SkipTags(data, ref pos, tagCount, poiTags);
            var featureByte = data[pos++];
            if ((featureByte & 0x80) != 0)
            {
                namePos = pos;
                SkipString(data, ref pos);
            }
            if ((featureByte & 0x40) != 0)
            {
                housenumberPos = pos;
                SkipString(data, ref pos);
            }
            if ((featureByte & 0x20) != 0)
            {
                // The elevation is stored as a signed integer
                elevationPos = pos;
                ReadVbeS(data, ref pos);
            }

            var firstPassEnd = pos;
            var x = MicrodegreesToTileX(lonMicro, requestedTile);
            var y = MicrodegreesToTileY(latMicro, requestedTile);
            if (x < ClipRectangle.MinX || x > ClipRectangle.MaxX || y < ClipRectangle.MinY || y > ClipRectangle.MaxY)
                continue;

            var point = CreatePoint(latMicro, lonMicro, requestedTile);

            // Second pass: decode the tags of the POI inside the requested tile; the tags and
            // values are consumed byte by byte as in the first pass, but the second pass ends
            // before the flags byte: POIs have no declared frame, so pos is restored to the
            // first pass end to keep the stream synchronized for the next POI
            pos = tagsPos;
            var tags = ReadTags(data, ref pos, tagCount, poiTags);
            if (namePos >= 0)
            {
                pos = namePos;
                tags["name"] = ReadString(data, ref pos);
            }
            if (housenumberPos >= 0)
            {
                pos = housenumberPos;
                tags["addr:housenumber"] = ReadString(data, ref pos);
            }
            if (elevationPos >= 0)
            {
                pos = elevationPos;
                tags["ele"] = ReadVbeS(data, ref pos).ToString();
            }

            pos = firstPassEnd;

            AddFeature(layers, _tagsConverter, point, tags);
        }

        // Decode the ways at the first way offset up to the count of the zoom table row
        pos = waysPos;
        for (long way = 0; way < wayCount && pos < end; way++)
        {
            if (hasDebugSignatures)
                // Debug signature of the way
                pos += DebugSignatureLength;

            // Way data size, counted from the sub tile bitmap; the fields are decoded
            // sequentially, but the declared size is kept as the frame of the way: if a way
            // is invalid or a decode step fails, the next way starts exactly at the frame
            // end, keeping the stream synchronized
            var wayDataSize = (int)ReadVbeu(data, ref pos);
            var wayEnd = pos + wayDataSize;

            // Sub-tile bitmap of the way, used for filtering in the origin format; skipped here
            pos += 2;

            var specialByte = data[pos++];
            var tagCount = specialByte & 0x0f;

            Dictionary<string, string>? tags = null;

            try
            {
                // First pass: skip through the tag and name fields, noting where they start;
                // the tags are decoded only for ways that reach the requested tile below. Like
                // the POIs, the flags byte follows the tag ids of the way
                var tagsPos = pos;
                var namePos = -1;
                var housenumberPos = -1;
                var referencePos = -1;
                SkipTags(data, ref pos, tagCount, wayTags);
                var featureByte = data[pos++];
                if ((featureByte & 0x80) != 0)
                {
                    namePos = pos;
                    SkipString(data, ref pos);
                }
                if ((featureByte & 0x40) != 0)
                {
                    housenumberPos = pos;
                    SkipString(data, ref pos);
                }
                if ((featureByte & 0x20) != 0)
                {
                    referencePos = pos;
                    SkipString(data, ref pos);
                }
                if ((featureByte & 0x10) != 0)
                {
                    // Label position: first the latitude offset, then the longitude offset; not used
                    ReadVbeS(data, ref pos);
                    ReadVbeS(data, ref pos);
                }

                var doubleDeltaEncoding = (featureByte & 0x04) != 0;
                var wayDataBlocks = (featureByte & 0x08) != 0 ? (int)ReadVbeu(data, ref pos) : 1;

                var wayDataBlocksCoordinates = ReadWayDataBlocks(data, ref pos, wayDataBlocks,
                    doubleDeltaEncoding, tileLatMicro, tileLonMicro, wayEnd);
                if (wayDataBlocksCoordinates == null ||
                    !wayDataBlocksCoordinates.Any(block => BlockIntersectsClip(block, requestedTile)))
                    // The way does not reach the requested tile: the tags stay undecoded
                    continue;

                // Second pass: decode the tags of the intersecting way
                pos = tagsPos;
                tags = ReadTags(data, ref pos, tagCount, wayTags);
                if (namePos >= 0)
                {
                    pos = namePos;
                    tags["name"] = ReadString(data, ref pos);
                }
                if (housenumberPos >= 0)
                {
                    pos = housenumberPos;
                    tags["addr:housenumber"] = ReadString(data, ref pos);
                }
                if (referencePos >= 0)
                {
                    pos = referencePos;
                    tags["ref"] = ReadString(data, ref pos);
                }

                AddFeature(layers, _tagsConverter, CreateWayGeometry(wayDataBlocksCoordinates, requestedTile, tags), tags);
            }
            catch (Exception exception) when (exception is InvalidDataException or IndexOutOfRangeException or ArgumentOutOfRangeException)
            {
                // Broken way data (e.g. an invalid tag id); the way is skipped
            }
            finally
            {
                // The frame end of the way data guarantees the synchronization for the next way
                pos = wayEnd;
            }
        }
    }

    /// <summary>
    /// Read the way data blocks of a way: each block holds the delta encoded node coordinates
    /// in microdegrees, relative to the top-left tile corner for the first node and to the
    /// previous node for the following nodes (single or double delta encoded).
    /// </summary>
    /// <param name="data">Byte array with the way data blocks at the given position</param>
    /// <param name="pos">Position of the way data blocks; points behind them after the call</param>
    /// <param name="wayDataBlocks">Number of way data blocks of the way</param>
    /// <param name="doubleDeltaEncoding">True, when the following nodes are double delta encoded</param>
    /// <param name="tileLatMicro">Latitude of the top-left tile corner in microdegrees</param>
    /// <param name="tileLonMicro">Longitude of the top-left tile corner in microdegrees</param>
    /// <param name="wayEnd">Declared frame end of the way data; decoding never reads beyond it</param>
    /// <returns>Decoded coordinate blocks in microdegrees; null when a way data block is invalid</returns>
    private static List<(long[] Latitudes, long[] Longitudes)>? ReadWayDataBlocks(byte[] data, ref int pos,
        int wayDataBlocks, bool doubleDeltaEncoding, long tileLatMicro, long tileLonMicro, int wayEnd)
    {
        var blocks = new List<(long[] Latitudes, long[] Longitudes)>(Math.Max(1, Math.Min(wayDataBlocks, 64)));
        try
        {
            for (var block = 0; block < wayDataBlocks && pos < wayEnd; block++)
            {
                // Each way data block starts with the number of following way coordinate blocks
                var coordinateBlockCount = (int)ReadVbeu(data, ref pos);
                for (var coordinateBlock = 0; coordinateBlock < coordinateBlockCount && pos < wayEnd; coordinateBlock++)
                {
                    var nodeCount = (int)ReadVbeu(data, ref pos);
                    if (nodeCount < 2)
                        // Invalid way coordinate block; no coordinates follow for it. Continue
                        // with the following blocks — aborting here would leave the stream
                        // position inside the block and desynchronize the whole tile decode
                        continue;
                    if (nodeCount * 2L > wayEnd - pos)
                        // The declared nodes do not fit into the way frame: invalid way data
                        return blocks.Count > 0 ? blocks : null;

                    var latitudes = new long[nodeCount];
                    var longitudes = new long[nodeCount];

                    latitudes[0] = tileLatMicro + ReadVbeS(data, ref pos);
                    longitudes[0] = tileLonMicro + ReadVbeS(data, ref pos);

                    long previousDeltaLat = 0, previousDeltaLon = 0;
                    for (var node = 1; node < nodeCount; node++)
                    {
                        var deltaLat = ReadVbeS(data, ref pos);
                        var deltaLon = ReadVbeS(data, ref pos);
                        if (doubleDeltaEncoding)
                        {
                            deltaLat += previousDeltaLat;
                            deltaLon += previousDeltaLon;
                        }

                        latitudes[node] = latitudes[node - 1] + deltaLat;
                        longitudes[node] = longitudes[node - 1] + deltaLon;
                        previousDeltaLat = deltaLat;
                        previousDeltaLon = deltaLon;
                    }

                    blocks.Add((latitudes, longitudes));
                }
            }

            return blocks;
        }
        catch (IndexOutOfRangeException)
        {
            // Broken way data: drop the rest of the way
            return blocks.Count > 0 ? blocks : null;
        }
    }

    /// <summary>
    /// Checks, if a base zoom tile intersects the requested tile: the corner coordinates of the
    /// base tile are transformed to the normalized tile space of the requested tile and their
    /// envelope is intersected with the <see cref="ClipRectangle"/>.
    /// </summary>
    private static bool BaseTileIntersectsRequestedTile(int baseTileX, int baseTileY, int baseZoom, Tile requestedTile)
    {
        var minLon = (long)Math.Round(TileXToLongitude(baseTileX, baseZoom) * 1_000_000.0);
        var maxLon = (long)Math.Round(TileXToLongitude(baseTileX + 1, baseZoom) * 1_000_000.0);
        var maxLat = (long)Math.Round(TileYToLatitude(baseTileY, baseZoom) * 1_000_000.0);
        var minLat = (long)Math.Round(TileYToLatitude(baseTileY + 1, baseZoom) * 1_000_000.0);

        var x1 = MicrodegreesToTileX(minLon, requestedTile);
        var x2 = MicrodegreesToTileX(maxLon, requestedTile);
        var y1 = MicrodegreesToTileY(maxLat, requestedTile);
        var y2 = MicrodegreesToTileY(minLat, requestedTile);

        var baseTileRectangle = new Envelope(Math.Min(x1, x2), Math.Max(x1, x2), Math.Min(y1, y2), Math.Max(y1, y2));
        return baseTileRectangle.Intersects(ClipRectangle);
    }

    /// <summary>
    /// Check, if a coordinate lies inside the <see cref="ClipRectangle"/>.
    /// </summary>
    private static bool IsInsideClip(Coordinate coordinate)
    {
        return coordinate.X >= ClipRectangle.MinX && coordinate.X <= ClipRectangle.MaxX &&
            coordinate.Y >= ClipRectangle.MinY && coordinate.Y <= ClipRectangle.MaxY;
    }

    /// <summary>
    /// Clip a line given as tile space coordinates to the <see cref="ClipRectangle"/>: each
    /// run of the line inside the rectangle becomes its own line string (path clipping after
    /// Cohen-Sutherland); runs of less than two points are dropped.
    /// </summary>
    private static List<Coordinate[]> ClipLineRuns(Coordinate[] coordinates)
    {
        var runs = new List<Coordinate[]>();
        List<Coordinate>? run = null;

        for (var i = 0; i < coordinates.Length - 1; i++)
        {
            var a = coordinates[i];
            var b = coordinates[i + 1];
            ClipSegment(out var clA, out var clB, a, b, out var aInside, out var bInside);

            if (aInside)
            {
                // When the run continues from the previous segment, its first point already
                // is this segment start
                run ??= [];
                if (run.Count == 0)
                    run.Add(clA);
                if (bInside)
                {
                    run.Add(clB);
                    continue;
                }

                runs.Add([.. run]);
                run = null;
                continue;
            }

            if (bInside)
            {
                // The segment enters the rectangle: start a run at the intersection
                run = [clA];
                run.Add(clB);
            }
            else if (SegmentCrossesRectangle(a, b))
            {
                // Both endpoints outside, but the segment crosses the rectangle: an isolated
                // run between the two rectangle intersections
                runs.Add([clA, clB]);
            }
        }

        // The last run of a line ending inside the rectangle is not terminated by a leaving
        // segment; it still reaches the rectangle end
        if (run is { Count: >= 2 })
            runs.Add([.. run]);

        return runs.Where(run => run.Length >= 2).ToList();
    }

    /// <summary>
    /// Clip one line segment to the <see cref="ClipRectangle"/>. Both flags state whether the
    /// original endpoints lie inside; on a crossing point, the intersection with the rectangle
    /// boundary is returned.
    /// </summary>
    private static void ClipSegment(out Coordinate clA, out Coordinate clB, Coordinate a, Coordinate b,
        out bool aInside, out bool bInside)
    {
        aInside = IsInsideClip(a);
        bInside = IsInsideClip(b);
        clA = a;
        clB = b;

        if (aInside && bInside || !aInside && !bInside && !SegmentCrossesRectangle(a, b))
            return;

        clA = aInside ? a : RectangleIntersection(a, b);
        clB = bInside ? b : RectangleIntersection(b, a);
    }

    /// <summary>
    /// Cheap check, if a segment with both endpoints outside the <see cref="ClipRectangle"/>
    /// switches sides of the rectangle (over-crosses it): the rectangle spans the coordinate
    /// ranges where such a crossing can take place.
    /// </summary>
    private static bool SegmentCrossesRectangle(Coordinate a, Coordinate b)
    {
        return SegmentCrossesVertical(a, b, ClipRectangle.MinX) &&
            SegmentCrossesVertical(a, b, ClipRectangle.MaxX) ||
            SegmentCrossesHorizontal(a, b, ClipRectangle.MinY) &&
            SegmentCrossesHorizontal(a, b, ClipRectangle.MaxY);
    }

    private static bool SegmentCrossesVertical(Coordinate a, Coordinate b, double x)
    {
        return (a.X <= x) != (b.X <= x);
    }

    private static bool SegmentCrossesHorizontal(Coordinate a, Coordinate b, double y)
    {
        return (a.Y <= y) != (b.Y <= y);
    }

    /// <summary>
    /// First intersection of the segment from a to b with one of the rectangle boundaries; the
    /// smallest positive parameter t wins. Only called for segments that cross the rectangle.
    /// </summary>
    private static Coordinate RectangleIntersection(Coordinate a, Coordinate b)
    {
        var t = FindFirstBoundaryCross(a, b);
        return Interpolate(a, b, t);
    }

    private static double FindFirstBoundaryCross(Coordinate a, Coordinate b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var best = double.MaxValue;

        var boundaries = new (double Position, bool Vertical)[]
        {
            (ClipRectangle.MinX, true),
            (ClipRectangle.MaxX, true),
            (ClipRectangle.MinY, false),
            (ClipRectangle.MaxY, false),
        };

        foreach (var (position, vertical) in boundaries)
        {
            double t;
            if (vertical)
            {
                if (dx == 0)
                    continue;
                t = (position - a.X) / dx;
            }
            else
            {
                if (dy == 0)
                    continue;
                t = (position - a.Y) / dy;
            }

            if (t < 0 || t > 1 || t >= best)
                continue;

            var point = Interpolate(a, b, t);
            var insideByOther = vertical
                ? point.Y >= ClipRectangle.MinY && point.Y <= ClipRectangle.MaxY
                : point.X >= ClipRectangle.MinX && point.X <= ClipRectangle.MaxX;
            if (!insideByOther)
                continue;

            best = t;
        }

        return best;
    }

    private static Coordinate Interpolate(Coordinate a, Coordinate b, double t)
    {
        return new Coordinate(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
    }

    /// <summary>
    /// Clip a closed ring to the <see cref="ClipRectangle"/> (Sutherland-Hodgman), used for
    /// polygons so a clipped polygon stays a proper ring. Returns null when the ring lies
    /// completely outside the rectangle or degenerates.
    /// </summary>
    private static Coordinate[]? ClipRing(Coordinate[] ring)
    {
        // Work with an open ring (the last node is not repeated)
        var output = new List<Coordinate>(ring.Length > 1 && ring[0].Equals2D(ring[^1]) ? ring[..^1] : ring);
        for (var edge = 0; edge < 4 && output.Count > 0; edge++)
        {
            output = ClipRingToEdge(output, edge);
        }

        return output.Count < 3 ? null : [.. output, output[0]];
    }

    /// <summary>
    /// Clip a ring to a single axis-aligned edge of the <see cref="ClipRectangle"/>. Edges are
    /// 0: minX, 1: maxX, 2: minY, 3: maxY; a side is inside when it touches the rectangle.
    /// </summary>
    private static List<Coordinate> ClipRingToEdge(List<Coordinate> ring, int edge)
    {
        bool Inside(Coordinate c) => edge switch
        {
            0 => c.X >= ClipRectangle.MinX,
            1 => c.X <= ClipRectangle.MaxX,
            2 => c.Y >= ClipRectangle.MinY,
            _ => c.Y <= ClipRectangle.MaxY,
        };

        var output = new List<Coordinate>(ring.Count);
        for (var i = 0; i < ring.Count; i++)
        {
            var current = ring[i];
            var previous = ring[(i + ring.Count - 1) % ring.Count];
            var currentInside = Inside(current);
            var previousInside = Inside(previous);

            if (currentInside)
            {
                if (!previousInside)
                    output.Add(EdgeIntersection(previous, current, edge));
                output.Add(current);
            }
            else if (previousInside)
            {
                output.Add(EdgeIntersection(previous, current, edge));
            }
        }

        return output;
    }

    /// <summary>
    /// Intersection of the segment previous-current with a boundary edge of the clip rectangle.
    /// </summary>
    private static Coordinate EdgeIntersection(Coordinate previous, Coordinate current, int edge)
    {
        return edge switch
        {
            0 => new Coordinate(ClipRectangle.MinX,
                previous.Y + (current.Y - previous.Y) * (ClipRectangle.MinX - previous.X) / (current.X - previous.X)),
            1 => new Coordinate(ClipRectangle.MaxX,
                previous.Y + (current.Y - previous.Y) * (ClipRectangle.MaxX - previous.X) / (current.X - previous.X)),
            2 => new Coordinate(
                previous.X + (current.X - previous.X) * (ClipRectangle.MinY - previous.Y) / (current.Y - previous.Y),
                ClipRectangle.MinY),
            _ => new Coordinate(
                previous.X + (current.X - previous.X) * (ClipRectangle.MaxY - previous.Y) / (current.Y - previous.Y),
                ClipRectangle.MaxY),
        };
    }

    /// <summary>
    /// Read the tag ids of a POI or way and resolve them to the tag strings of the given tag
    /// string table of the file header. The tag values of the table are 'key=value' strings;
    /// value wildcards as of file format version 5 (e.g. 'ele=%i') are resolved to the value
    /// stored with the tag id.
    /// </summary>
    /// <param name="data">Byte array with the tag ids at the given position</param>
    /// <param name="pos">Position of the tag ids; points behind them after the call</param>
    /// <param name="tagCount">Number of tag ids</param>
    /// <param name="tagTable">Tag string table of the file header</param>
    /// <returns>Decoded tags as 'key' to 'value' map</returns>
    /// <exception cref="InvalidDataException">Thrown when a tag id refers to a tag that only
    /// exists in newer file format versions</exception>
    private static Dictionary<string, string> ReadTags(byte[] data, ref int pos, int tagCount, string[] tagTable)
    {
        var tags = new Dictionary<string, string>(tagCount);

        // The tag ids refer to the tag string table; like the mapsforge reference, all tag ids
        // are read first and only then the variable tag values are resolved, so the variable
        // value of a tag follows the whole list of tag ids in the data
        var tagIds = new int[tagCount];
        for (var i = 0; i < tagCount; i++)
        {
            var tagId = (int)ReadVbeu(data, ref pos);
            if (tagId < 0 || tagId >= tagTable.Length)
                throw new InvalidDataException($"Invalid tag id {tagId} in mapsforge tile data");

            tagIds[i] = tagId;
        }

        foreach (var tagId in tagIds)
        {
            // The tag strings of the table are 'key=value'
            var tag = tagTable[tagId];
            var separator = tag.IndexOf('=');
            var key = separator < 0 ? tag : tag[..separator];
            var value = separator < 0 ? string.Empty : tag[(separator + 1)..];

            // Variable tag values (file format version 5): the table value is a wildcard like
            // '%i', the actual value follows the tag id in the type given by the wildcard
            // character (%b byte, %i int, %f float, %h short, %s string)
            if (value.Length == 2 && value[0] == '%')
            {
                switch (value[1])
                {
                    case 'b':
                        value = ((sbyte)data[pos++]).ToString();
                        break;
                    case 'i':
                        value = key.Contains(":colour")
                            ? "#" + ReadInt(data, ref pos).ToString("x")
                            : ReadInt(data, ref pos).ToString();
                        break;
                    case 'f':
                        value = ReadFloat(data, ref pos).ToString("R");
                        break;
                    case 'h':
                        value = ((short)((data[pos] << 8) | data[pos + 1])).ToString();
                        pos += 2;
                        break;
                    case 's':
                        value = ReadString(data, ref pos);
                        break;
                    default:
                        throw new InvalidDataException($"Invalid variable tag value '%{value[1]}' in mapsforge tile data");
                }
            }

            tags[key] = value;
        }

        return tags;
    }

    /// <summary>
    /// Skip the tag block of a POI or way without decoding it: reads the tag ids and overreads
    /// the variable tag values. Used when the feature does not reach the requested tile, so its
    /// tags are never needed.
    /// </summary>
    /// <param name="data">Byte array with the tag ids at the given position</param>
    /// <param name="pos">Position of the tag ids; points behind the variable tag values after
    /// the call</param>
    /// <param name="tagCount">Number of tag ids</param>
    /// <param name="tagTable">Tag string table of the file header</param>
    private static void SkipTags(byte[] data, ref int pos, int tagCount, string[] tagTable)
    {
        // The tag ids refer to the tag string table; like ReadTags, all tag ids are read first
        // and only then the variable tag values, which follow the whole list of tag ids
        var tagIds = new int[tagCount];
        for (var i = 0; i < tagCount; i++)
            tagIds[i] = (int)ReadVbeu(data, ref pos);

        foreach (var tagId in tagIds)
        {
            if (tagId < 0 || tagId >= tagTable.Length)
                // An invalid tag id leaves the variable values unread; the frame end of the way
                // resynchronizes the stream anyway
                return;

            var tag = tagTable[tagId];
            var separator = tag.IndexOf('=');
            if (separator < 0 || tag.Length - separator != 3 || tag[separator + 1] != '%')
                continue;

            switch (tag[^1])
            {
                case 'b':
                    pos += 1;
                    break;
                case 'h':
                    pos += 2;
                    break;
                case 'i':
                case 'f':
                    pos += 4;
                    break;
                case 's':
                {
                    var length = (int)ReadVbeu(data, ref pos);
                    pos += length;
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Skip a string without decoding it: a VBE-U INT length followed by the bytes of the
    /// string.
    /// </summary>
    private static void SkipString(byte[] data, ref int pos)
    {
        var length = (int)ReadVbeu(data, ref pos);
        pos += length;
    }

    /// <summary>
    /// Add a raw feature with the given geometry and tags to the layers: the source layer and
    /// the attributes of the feature follow from the tags converter.
    /// </summary>
    /// <param name="layers">Layers to add the feature to</param>
    /// <param name="tagsConverter">Tags and source layer converter of the tile conversion</param>
    /// <param name="geometry">Geometry of the feature, in the normalized tile space</param>
    /// <param name="tags">Raw tags of the feature</param>
    private static void AddFeature(Dictionary<string, Layer> layers, ITagsConverter? tagsConverter,
        Geometry geometry, Dictionary<string, string> tags)
    {
        var attributes = new AttributesTable();
        foreach (var (key, value) in tags)
        {
            attributes.Add(key, value);
        }

        AddConvertedFeature(layers, tagsConverter, geometry, string.Empty, attributes);
    }

    /// <summary>
    /// Convert the tags and the source layer of a feature with the tags converter and add the
    /// feature to the layer of its converted source layer.
    /// </summary>
    /// <param name="layers">Layers to add the feature to</param>
    /// <param name="tagsConverter">Tags and source layer converter of the tile conversion; null
    /// uses the <see cref="MapsforgeTagsConverter"/> with the OpenMapTiles schema</param>
    /// <param name="geometry">Geometry of the feature, in the normalized tile space</param>
    /// <param name="sourceLayer">Provisional source layer of the feature</param>
    /// <param name="attributes">Raw or preconverted tags of the feature</param>
    private static void AddConvertedFeature(Dictionary<string, Layer> layers, ITagsConverter? tagsConverter,
        Geometry geometry, string sourceLayer, IAttributesTable attributes)
    {
        var conversions = tagsConverter != null
            ? tagsConverter.ConvertTags(geometry, sourceLayer, attributes)
            : MapsforgeTagsConverter.ConvertTags(geometry, sourceLayer, attributes);

        foreach (var (convertedLayer, convertedAttributes) in conversions)
        {
            if (string.IsNullOrEmpty(convertedLayer))
                // The tags converter dropped the feature
                continue;

            if (!layers.TryGetValue(convertedLayer, out var layer))
            {
                layer = new Layer
                {
                    Name = convertedLayer,
                };
                layers.Add(convertedLayer, layer);
            }

            layer.Features.Add(new Feature
            {
                Geometry = geometry,
                Attributes = convertedAttributes,
            });
        }
    }

    /// <summary>
    /// Create the geometry of a POI: a point at the transformed position.
    /// </summary>
    /// <param name="latMicro">Latitude in microdegrees</param>
    /// <param name="lonMicro">Longitude in microdegrees</param>
    /// <param name="requestedTile">Tile requested by the renderer; the target of the coordinate transformation</param>
    /// <returns>Point in the normalized tile space of the requested tile</returns>
    private static Point CreatePoint(long latMicro, long lonMicro, Tile requestedTile)
    {
        return GeometryFactory.CreatePoint(new Coordinate(MicrodegreesToTileX(lonMicro, requestedTile),
            MicrodegreesToTileY(latMicro, requestedTile)));
    }

    /// <summary>
    /// Create the geometry of a way: closed coordinate blocks with area-like tags become
    /// polygons and else line strings; multiple coordinate blocks become a multi geometry.
    /// </summary>
    /// <param name="blocks">Decoded coordinate blocks of the way in microdegrees</param>
    /// <param name="requestedTile">Tile requested by the renderer; the target of the coordinate transformation</param>
    /// <param name="tags">Raw tags of the way</param>
    /// <returns>Line or area geometry in the normalized tile space of the requested tile</returns>
    private static Geometry CreateWayGeometry(List<(long[] Latitudes, long[] Longitudes)> blocks, Tile requestedTile,
        Dictionary<string, string> tags)
    {
        // Mapsforge does not store the geometry type of a way; the classification follows the
        // conventions of the OpenStreetMap tags: a way is an area when all of its coordinate
        // blocks are closed rings and it carries an area-like tag or an explicit 'area=yes'
        var isClosedRing = blocks.All(b => IsClosedRing(b.Latitudes, b.Longitudes));
        var isArea = isClosedRing &&
            (tags.TryGetValue("area", out var area) && area == "yes" ||
             tags.Keys.Any(key => PolygonTagKeys.Contains(key) && tags[key] != "no" &&
                !(key == "natural" && tags[key] == "coastline")));

        // Blocks whose extent misses the clip rectangle are skipped before any geometry is
        // transformed or built: an overzoom tile covers only a small fraction of the data of
        // its base tiles
        var relevantBlocks = blocks.Where(b => BlockIntersectsClip(b, requestedTile)).ToList();
        if (relevantBlocks.Count == 0)
            return GeometryFactory.CreateGeometryCollection([]);

        var geometries = isArea
            ? relevantBlocks.Select(b => CreatePolygon(b, requestedTile)).Where(g => !g.IsEmpty).ToArray()
            : relevantBlocks.SelectMany(b => CreateLineString(b, requestedTile)).ToArray();

        return geometries.Length switch
        {
            0 => GeometryFactory.CreateGeometryCollection([]),
            1 => geometries[0],
            _ => isArea
                ? GeometryFactory.CreateMultiPolygon(geometries.OfType<Polygon>().ToArray())
                : GeometryFactory.CreateMultiLineString(geometries.OfType<LineString>().ToArray()),
        };
    }

    /// <summary>
    /// Check, if the extent of a way data block intersects the clip rectangle: the coordinate
    /// transformation is monotone in latitude and longitude, so the transformed extent follows
    /// directly from the microdegree extremes of the block.
    /// </summary>
    private static bool BlockIntersectsClip((long[] Latitudes, long[] Longitudes) block, Tile requestedTile)
    {
        if (block.Latitudes.Length == 0)
            return false;

        var minLat = long.MaxValue;
        var maxLat = long.MinValue;
        var minLon = long.MaxValue;
        var maxLon = long.MinValue;
        for (var i = 0; i < block.Latitudes.Length; i++)
        {
            minLat = Math.Min(minLat, block.Latitudes[i]);
            maxLat = Math.Max(maxLat, block.Latitudes[i]);
            minLon = Math.Min(minLon, block.Longitudes[i]);
            maxLon = Math.Max(maxLon, block.Longitudes[i]);
        }

        var x1 = MicrodegreesToTileX(minLon, requestedTile);
        var x2 = MicrodegreesToTileX(maxLon, requestedTile);
        var y1 = MicrodegreesToTileY(maxLat, requestedTile);
        var y2 = MicrodegreesToTileY(minLat, requestedTile);

        var extent = new Envelope(Math.Min(x1, x2), Math.Max(x1, x2), Math.Min(y1, y2), Math.Max(y1, y2));
        return extent.Intersects(ClipRectangle);
    }

    /// <summary>
    /// Tag keys that declare area-like features when the way forms a closed ring.
    /// </summary>
    private static readonly HashSet<string> PolygonTagKeys = new(StringComparer.Ordinal)
    {
        "building",
        "building:part",
        "landuse",
        "leisure",
        "amenity",
        "natural",
        "boundary",
        "aeroway",
        "military",
        "tourism",
        "sport",
        "place",
    };

    /// <summary>
    /// Check, if a coordinate block forms a closed ring.
    /// </summary>
    private static bool IsClosedRing(long[] latitudes, long[] longitudes)
    {
        return latitudes.Length > 2 && latitudes[0] == latitudes[^1] && longitudes[0] == longitudes[^1];
    }

    /// <summary>
    /// Transforms the coordinates of one way data block to the normalized tile space and clips
    /// them to the requested tile; each run of the way inside the tile becomes a line string.
    /// </summary>
    private static IEnumerable<LineString> CreateLineString((long[] Latitudes, long[] Longitudes) block,
        Tile requestedTile)
    {
        return ClipLineRuns(TransformBlock(block, requestedTile))
            .Select(run => GeometryFactory.CreateLineString(run));
    }

    /// <summary>
    /// Transforms the coordinates of one way data block to the normalized tile space and clips
    /// them to the requested tile; rings outside the tile return an empty geometry.
    /// </summary>
    private static Geometry CreatePolygon((long[] Latitudes, long[] Longitudes) block, Tile requestedTile)
    {
        var clipped = ClipRing(TransformBlock(block, requestedTile));
        return clipped == null
            ? GeometryFactory.CreatePolygon([])
            : GeometryFactory.CreatePolygon(clipped);
    }

    /// <summary>
    /// Transforms the microdegree coordinates of one way data block to the normalized tile
    /// space of the requested tile.
    /// </summary>
    private static Coordinate[] TransformBlock((long[] Latitudes, long[] Longitudes) block, Tile requestedTile)
    {
        var coordinates = new Coordinate[block.Latitudes.Length];
        for (var i = 0; i < block.Latitudes.Length; i++)
        {
            coordinates[i] = new Coordinate(MicrodegreesToTileX(block.Longitudes[i], requestedTile),
                MicrodegreesToTileY(block.Latitudes[i], requestedTile));
        }

        return coordinates;
    }

    /// <summary>
    /// Longitude in degrees of the top-left corner of a tile in the XYZ scheme.
    /// </summary>
    private static double TileXToLongitude(int tileX, int zoom)
    {
        return tileX / Math.Pow(2, zoom) * 360.0 - 180.0;
    }

    /// <summary>
    /// Latitude in degrees of the top-left corner of a tile in the XYZ scheme.
    /// </summary>
    private static double TileYToLatitude(int tileY, int zoom)
    {
        var n = Math.PI * (1.0 - 2.0 * tileY / Math.Pow(2, zoom));
        return Math.Atan(Math.Sinh(n)) * 180.0 / Math.PI;
    }

    /// <summary>
    /// X coordinate of a longitude in microdegrees in the normalized tile space of the
    /// requested tile.
    /// </summary>
    private static double MicrodegreesToTileX(long lonMicro, Tile requestedTile)
    {
        var longitude = lonMicro / 1_000_000.0;
        return (longitude + 180.0) / 360.0 * TilePixelSize(requestedTile.Zoom) - requestedTile.X * 512.0;
    }

    /// <summary>
    /// Y coordinate of a latitude in microdegrees in the normalized tile space of the requested
    /// tile (top-left tile origin, y growing downwards, as in the OpenMapTiles vector tiles).
    /// </summary>
    private static double MicrodegreesToTileY(long latMicro, Tile requestedTile)
    {
        var radians = latMicro / 1_000_000.0 * Math.PI / 180.0;
        var mercatorY = (1.0 - Math.Log(Math.Tan(Math.PI / 4.0 + radians / 2.0)) / Math.PI) / 2.0;
        return mercatorY * TilePixelSize(requestedTile.Zoom) - requestedTile.Y * 512.0;
    }

    /// <summary>
    /// Pixel size of a tile at the given zoom level in the normalized 512x512 tile space.
    /// </summary>
    private static double TilePixelSize(int zoom)
    {
        return 512.0 * Math.Pow(2, zoom);
    }

    /// <summary>
    /// Read an unsigned VBE-U INT at the current position: the first bit of each byte is the
    /// continuation bit, each byte holds seven bits of the value, starting with the least
    /// significant ones.
    /// </summary>
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
    /// Read a signed VBE-S INT at the current position: like the VBE-U INT, but with the sign
    /// in the second bit of the last byte and only six data bits in the last byte.
    /// </summary>
    private static long ReadVbeS(byte[] data, ref int pos)
    {
        long value = 0;
        var shift = 0;
        byte currentByte;
        while (true)
        {
            currentByte = data[pos++];
            if ((currentByte & 0x80) == 0)
            {
                // Last byte: six data bits at the current shift and the sign in bit 6
                value |= (long)(currentByte & 0x3f) << shift;
                return (currentByte & 0x40) != 0 ? -value : value;
            }

            value |= (long)(currentByte & 0x7f) << shift;
            shift += 7;
        }
    }

    /// <summary>
    /// Read a 4-byte big endian int at the current position.
    /// </summary>
    private static int ReadInt(byte[] data, ref int pos)
    {
        var value = data[pos] << 24 | data[pos + 1] << 16 | data[pos + 2] << 8 | data[pos + 3];
        pos += 4;
        return value;
    }

    /// <summary>
    /// Read a 4-byte big endian float at the current position.
    /// </summary>
    private static float ReadFloat(byte[] data, ref int pos)
    {
        return BitConverter.Int32BitsToSingle(ReadInt(data, ref pos));
    }

    /// <summary>
    /// Read a string at the current position: a VBE-U INT with the length of the UTF-8 encoded
    /// string in bytes, followed by the bytes of the string.
    /// </summary>
    private static string ReadString(byte[] data, ref int pos)
    {
        var length = (int)ReadVbeu(data, ref pos);
        var value = Encoding.UTF8.GetString(data, pos, length);
        pos += length;
        return value;
    }

    /// <summary>
    /// Read a string table at the current position: a VBE-U INT with the number of strings,
    /// followed by each string as VBE-U INT length + UTF-8 bytes.
    /// </summary>
    private static string[] ReadStringTable(byte[] data, ref int pos)
    {
        var count = (int)ReadVbeu(data, ref pos);
        var strings = new string[count];
        for (var i = 0; i < count; i++)
            strings[i] = ReadString(data, ref pos);

        return strings;
    }
}
