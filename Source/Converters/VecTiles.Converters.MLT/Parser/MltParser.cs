using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;

namespace VecTiles.Converters.MLT.Parser;

/// <summary>
/// Parses tiles in MapLibre Tile (MLT) v1 format into <see cref="VectorTile"/> instances.
/// </summary>
public static class MltParser
{
    /// <summary>
    /// Name of the attribute the feature identifier is stored under.
    /// </summary>
    public static string Id = "id";

    /// <summary>
    /// Tag of a layer frame in an MLT tile.
    /// </summary>
    private const byte LayerFrameTag = 1;

    /// <summary>
    /// Parses an unzipped tile in MLT format.
    /// </summary>
    /// <param name="data">Binary array with the tile data in MLT format</param>
    /// <param name="requestedTile">The tile being requested.</param>
    /// <param name="providedTile">The tile provided in the data stream.</param>
    /// <param name="tagsConverter">Optional converter for the tags and source layers of the
    /// features to the conventions of the style rendering the converted tile. When null, or a
    /// strict <see cref="IdentityTagsConverter"/>, the tags and source layers of the data are
    /// kept unchanged (identity conversion).</param>
    /// <returns>A VectorTile containing layers and features parsed from the data.</returns>
    public static VectorTile Parse(ReadOnlyMemory<byte> data, Tile requestedTile, Tile providedTile, ITagsConverter? tagsConverter = null)
    {
        if (data.Length == 0)
        {
            // If the data is empty, return an empty VectorTile
            return new VectorTile { TileId = requestedTile.Id };
        }

        // Overzoom transformation if needed
        var overzoom = requestedTile.Zoom == providedTile.Zoom
            ? Overzoom.None
            : Overzoom.CreateFromTiles(requestedTile, providedTile);

        // A strict identity tags converter keeps the source layer of a feature as delivered by
        // the data, like no converter at all, so the features can stay in the layers of the
        // data. Any other tags converter derives the source layer of a feature from its tags,
        // so the features are collected in layers by the converted source layer name
        var isIdentity = tagsConverter == null || tagsConverter is IdentityTagsConverter { FallbackSourceLayer: null };
        var layersByName = isIdentity ? null : new Dictionary<string, Layer>();

        var vectorTile = new VectorTile { TileId = requestedTile.Id };
        var reader = new MltReader(data.ToArray());

        // A tile is a sequence of frames, each a varint size, a tag byte and a body;
        // frames with the layer tag hold a layer
        while (reader.Position < data.Length)
        {
            var size = (long)reader.Varint();
            if (size < 1)
                throw new InvalidDataException("MLT frame size below 1 byte");
            var tag = reader.Byte();
            var body = reader.Bytes(size - 1);
            if (tag == LayerFrameTag)
            {
                ParseLayer(body, overzoom, tagsConverter, isIdentity, layersByName, vectorTile);
            }
        }

        if (layersByName != null)
        {
            foreach (var vectorTileLayer in layersByName.Values)
            {
                vectorTile.Layers.Add(vectorTileLayer);
            }
        }

        return vectorTile;
    }

    /// <summary>
    /// Parses one layer frame and adds its features to the vector tile, either as a layer of
    /// its own (identity conversion) or distributed to the layers of the converted source
    /// layer names.
    /// </summary>
    private static void ParseLayer(byte[] body, Overzoom overzoom, ITagsConverter? tagsConverter,
        bool isIdentity, Dictionary<string, Layer>? layersByName, VectorTile vectorTile)
    {
        var reader = new MltReader(body);
        var name = reader.String();
        var extent = (uint)reader.Varint();
        var columnCount = (long)reader.Varint();
        var metas = ReadColumnMetas(reader, columnCount);

        // The columns are decoded in metadata order, the order of the data section
        long[]? ids = null;
        long[]? geometryTypes = null;
        long[]? geometries = null;
        long[]? parts = null;
        long[]? rings = null;
        long[]? vertices = null;
        var attributeColumns = new List<AttributeColumn>();

        foreach (var meta in metas)
        {
            switch (meta.Type)
            {
                case 0: // Id
                case 1: // OptId
                case 2: // LongId
                case 3: // OptLongId
                    ids = DecodeIdColumn(reader, meta.Type);
                    break;

                case 4: // Geometry
                    (geometryTypes, geometries, parts, rings, vertices) = DecodeGeometryColumn(reader);
                    break;

                case 10: // Bool
                case 11: // OptBool
                case 16: // Int32
                case 17: // OptInt32
                case 18: // UInt32
                case 19: // OptUInt32
                case 20: // Int64
                case 21: // OptInt64
                case 22: // UInt64
                case 23: // OptUInt64
                case 24: // Float
                case 25: // OptFloat
                case 26: // Double
                case 27: // OptDouble
                    attributeColumns.Add(DecodeScalarColumn(reader, meta));
                    break;

                case 28: // String
                case 29: // OptString
                    attributeColumns.Add(DecodeStringColumn(reader, meta));
                    break;

                case 30: // SharedDict
                    attributeColumns.AddRange(DecodeSharedDictColumn(reader, meta));
                    break;

                default:
                    throw new InvalidDataException($"Unassigned MLT column type {meta.Type}");
            }
        }

        if (geometryTypes == null || vertices == null)
        {
            // A layer must contain a geometry column; without one there are no features
            return;
        }

        // The feature count comes from the geometry types stream
        var featureCount = geometryTypes.Length;
        var layerOverzoom = overzoom.WithExtent(extent);
        var decodedGeometries = GeometryDecoder.Decode(geometryTypes, geometries, parts, rings, vertices, layerOverzoom);

        // The extent of the coordinate space is defined per layer in the MLT format
        var vectorTileLayer = isIdentity
            ? new Layer(featureCount)
            {
                Name = name,
            }
            : null;

        for (var i = 0; i < featureCount; i++)
        {
            var geometry = decodedGeometries[i];
            if (geometry == null)
            {
                // Features without a usable geometry or outside of the tile are skipped
                continue;
            }

            var attributes = new AttributesTable();
            foreach (var column in attributeColumns)
            {
                if (column.Present != null && i < column.Present.Length && !column.Present[i])
                {
                    // An absent value of a nullable column omits the attribute
                    continue;
                }

                if (i >= column.Values.Length)
                {
                    continue;
                }

                var value = column.Values[i];
                if (value != null)
                {
                    attributes.Add(column.Name, value);
                }
            }

            if (ids != null && i < ids.Length && ids[i] != 0 && !attributes.Exists(Id))
            {
                // Like the MVT parser, the id is stored as ulong, so the symbol
                // factories can unbox it without a cast exception
                attributes.Add(Id, unchecked((ulong)ids[i]));
            }

            if (vectorTileLayer != null)
            {
                vectorTileLayer.Features.Add(new Feature
                {
                    Geometry = geometry,
                    Attributes = attributes,
                });
            }
            else
            {
                var conversions = tagsConverter!.ConvertTags(geometry, name, attributes);

                foreach (var (sourceLayer, convertedAttributes) in conversions)
                {
                    if (!layersByName!.TryGetValue(sourceLayer, out var targetLayer))
                    {
                        targetLayer = new Layer
                        {
                            Name = sourceLayer,
                        };
                        layersByName.Add(sourceLayer, targetLayer);
                    }

                    targetLayer.Features.Add(new Feature
                    {
                        Geometry = geometry,
                        Attributes = convertedAttributes,
                    });
                }
            }
        }

        if (vectorTileLayer != null)
        {
            // Add the layer to the VectorTile
            vectorTile.Layers.Add(vectorTileLayer);
        }
    }

    /// <summary>
    /// One decoded attribute column: its name, the presence bits of a nullable column and
    /// the values.
    /// </summary>
    private sealed record AttributeColumn(string Name, bool[]? Present, object?[] Values);

    private sealed record ColumnMeta(int Type, string? Name, List<ColumnMeta> Children);

    /// <summary>
    /// Reads the column metadata entries.
    /// </summary>
    private static List<ColumnMeta> ReadColumnMetas(MltReader reader, long count)
    {
        var metas = new List<ColumnMeta>((int)count);
        for (var i = 0; i < count; i++)
        {
            var type = reader.Byte();
            string? name = null;
            if (type is not (0 or 1 or 2 or 3 or 4))
            {
                name = reader.String();
            }

            var children = new List<ColumnMeta>();
            if (type == 30)
            {
                var childCount = (long)reader.Varint();
                for (var c = 0; c < childCount; c++)
                {
                    var childType = reader.Byte();
                    var childName = reader.String();
                    children.Add(new ColumnMeta(childType, childName, []));
                }
            }

            metas.Add(new ColumnMeta(type, name, children));
        }

        return metas;
    }

    /// <summary>
    /// Decodes an id column; the ids of absent values become 0 so the id attribute is omitted.
    /// </summary>
    private static long[] DecodeIdColumn(MltReader reader, int columnType)
    {
        bool[]? present = null;
        if (columnType % 2 == 1)
        {
            present = StreamDecoder.DecodeBooleanStream(MltStream.Read(reader, boolean: true));
        }

        var data = MltStream.Read(reader);
        var wordBytes = columnType is 2 or 3 ? 8 : 4;
        var values = StreamDecoder.DecodeIntegerStream(data, signed: false, wordBytes);
        if (present != null)
        {
            for (var i = 0; i < values.Length && i < present.Length; i++)
            {
                if (!present[i])
                {
                    values[i] = 0;
                }
            }
        }

        return values;
    }

    /// <summary>
    /// Decodes the geometry column: a stream count, the types stream, then the topology and
    /// vertex streams, identified by their stream type.
    /// </summary>
    private static (long[] Types, long[]? Geometries, long[]? Parts, long[]? Rings, long[] Vertices)
        DecodeGeometryColumn(MltReader reader)
    {
        var streamCount = (long)reader.Varint();
        var types = StreamDecoder.DecodeIntegerStream(MltStream.Read(reader));
        var streams = new Dictionary<int, MltStream>();
        for (var i = 1; i < streamCount; i++)
        {
            var stream = MltStream.Read(reader);
            streams[stream.StreamType] = stream;
        }

        streams.TryGetValue(0x31, out var geometriesStream);
        streams.TryGetValue(0x32, out var partsStream);
        streams.TryGetValue(0x33, out var ringsStream);
        streams.TryGetValue(0x20, out var offsetsStream);
        streams.TryGetValue(0x13, out var vertexStream);
        streams.TryGetValue(0x14, out var mortonStream);

        var geometries = geometriesStream != null ? StreamDecoder.DecodeIntegerStream(geometriesStream) : null;
        var parts = partsStream != null ? StreamDecoder.DecodeIntegerStream(partsStream) : null;
        var rings = ringsStream != null ? StreamDecoder.DecodeIntegerStream(ringsStream) : null;

        // Vertices are either stored directly or as indices into a vertex dictionary
        long[] vertices;
        if (offsetsStream != null)
        {
            var indices = StreamDecoder.DecodeIntegerStream(offsetsStream);
            var dictionary = vertexStream != null
                ? StreamDecoder.DecodeIntegerStream(vertexStream)
                : mortonStream != null
                    ? StreamDecoder.DecodeIntegerStream(mortonStream)
                    : throw new InvalidDataException("MLT vertex dictionary missing");
            vertices = new long[indices.Length * 2];
            for (var i = 0; i < indices.Length; i++)
            {
                var dictionaryIndex = indices[i] * 2;
                if (dictionaryIndex + 1 >= dictionary.Length)
                    throw new InvalidDataException($"MLT vertex dictionary index {indices[i]} outside the dictionary");
                vertices[2 * i] = dictionary[dictionaryIndex];
                vertices[2 * i + 1] = dictionary[dictionaryIndex + 1];
            }
        }
        else if (vertexStream != null)
        {
            vertices = StreamDecoder.DecodeIntegerStream(vertexStream);
        }
        else if (mortonStream != null)
        {
            vertices = StreamDecoder.DecodeIntegerStream(mortonStream);
        }
        else
        {
            throw new InvalidDataException("MLT vertex buffer missing");
        }

        return (types, geometries, parts, rings, vertices);
    }

    /// <summary>
    /// Decodes a scalar column: the values of Bool, Int32, UInt32, Int64, UInt64, Float and
    /// Double columns; nullable columns prefix a Present stream.
    /// </summary>
    private static AttributeColumn DecodeScalarColumn(MltReader reader, ColumnMeta meta)
    {
        bool[]? present = null;
        if (meta.Type % 2 == 1)
        {
            present = StreamDecoder.DecodeBooleanStream(MltStream.Read(reader, boolean: true));
        }

        var data = MltStream.Read(reader, boolean: meta.Type is 10 or 11);
        object?[] values;
        switch (meta.Type)
        {
            case 10 or 11: // Bool
                values = [.. StreamDecoder.DecodeBooleanStream(data).Select(b => (object?)b)];
                break;

            case 16 or 17: // Int32
                values = [.. StreamDecoder.DecodeIntegerStream(data, signed: true).Select(v => (object?)v)];
                break;

            case 18 or 19: // UInt32
                values = [.. StreamDecoder.DecodeIntegerStream(data).Select(v => (object?)v)];
                break;

            case 20 or 21: // Int64
                values = [.. StreamDecoder.DecodeIntegerStream(data, signed: true, 8).Select(v => (object?)v)];
                break;

            case 22 or 23: // UInt64
                values = [.. StreamDecoder.DecodeIntegerStream(data, signed: false, 8).Select(v => (object?)v)];
                break;

            case 24 or 25: // Float
                values = [.. StreamDecoder.DecodeFloatStream(data, doublePrecision: false).Select(v => (object?)v)];
                break;

            default: // 26 or 27, Double
                values = [.. StreamDecoder.DecodeFloatStream(data, doublePrecision: true).Select(v => (object?)v)];
                break;
        }

        return new AttributeColumn(meta.Name!, present, values);
    }

    /// <summary>
    /// Decodes a string column: plain, dictionary encoded, FSST compressed or a combination
    /// of FSST and dictionary, recognizable by the stream types after the optional Present
    /// stream.
    /// </summary>
    private static AttributeColumn DecodeStringColumn(MltReader reader, ColumnMeta meta)
    {
        var streamCount = (long)reader.Varint();
        var streams = new List<MltStream>((int)streamCount);
        for (var i = 0; i < streamCount; i++)
        {
            // The Present stream of an optional string column carries no run count
            var boolean = streams.Count == 0 && meta.Type == 29;
            streams.Add(MltStream.Read(reader, boolean));
        }

        bool[]? present = null;
        var first = 0;
        if (meta.Type == 29 && streams.Count > 0)
        {
            present = StreamDecoder.DecodeBooleanStream(streams[0]);
            first = 1;
        }

        var rest = streams.Skip(first).ToList();
        long[]? lengths = null;
        long[]? codes = null;
        byte[] blob;

        switch (rest.Count)
        {
            case 2: // plain: Length/VarBinary, Data/None
                lengths = StreamDecoder.DecodeIntegerStream(rest[0]);
                blob = rest[1].Payload;
                break;

            case 3: // dictionary: Length/Dictionary, Offset/String, Data/Single
                lengths = StreamDecoder.DecodeIntegerStream(rest[0]);
                codes = StreamDecoder.DecodeIntegerStream(rest[1]);
                blob = rest[2].Payload;
                break;

            case 4: // FSST: Length/Symbol, Data/FSST, Length/Dictionary, Data/Single
                var symbolLengths = StreamDecoder.DecodeIntegerStream(rest[0]);
                lengths = StreamDecoder.DecodeIntegerStream(rest[2]);
                blob = FsstExpand(symbolLengths, rest[1].Payload, rest[3].Payload);
                break;

            case 5: // FSST dictionary: Length/Symbol, Data/FSST, Length/Dictionary, Data/Shared, Offset/String
                var fsstSymbolLengths = StreamDecoder.DecodeIntegerStream(rest[0]);
                lengths = StreamDecoder.DecodeIntegerStream(rest[2]);
                codes = StreamDecoder.DecodeIntegerStream(rest[4]);
                blob = FsstExpand(fsstSymbolLengths, rest[1].Payload, rest[3].Payload);
                break;

            default:
                throw new InvalidDataException($"Unsupported MLT string column layout with {rest.Count} streams");
        }

        var strings = SplitBlob(blob, lengths!);
        var values = new string?[codes != null ? codes.Length : strings.Length];
        for (var i = 0; i < values.Length; i++)
        {
            var code = codes != null ? codes[i] : i;
            if (code >= 0 && code < strings.Length)
            {
                values[i] = strings[code];
            }
        }

        return new AttributeColumn(meta.Name!, present, values);
    }

    /// <summary>
    /// Decodes a shared dictionary column: a string dictionary followed by the columns
    /// indexing into it, each with an optional Present stream.
    /// </summary>
    private static List<AttributeColumn> DecodeSharedDictColumn(MltReader reader, ColumnMeta meta)
    {
        var streamCount = (long)reader.Varint();

        // The dictionary streams are read until the data stream of the dictionary
        var dictionaryStreams = new List<MltStream>();
        while (true)
        {
            var stream = MltStream.Read(reader);
            dictionaryStreams.Add(stream);
            if (stream.StreamType is 0x11 or 0x12)
            {
                break;
            }
        }

        string[] dictionary;
        if (dictionaryStreams.Count == 2)
        {
            // plain: Length/Dictionary, Data/Shared
            var lengths = StreamDecoder.DecodeIntegerStream(dictionaryStreams[0]);
            dictionary = SplitBlob(dictionaryStreams[1].Payload, lengths);
        }
        else if (dictionaryStreams.Count == 4)
        {
            // FSST: Length/Symbol, Data/FSST, Length/Dictionary, Data/Single
            var symbolLengths = StreamDecoder.DecodeIntegerStream(dictionaryStreams[0]);
            var lengths = StreamDecoder.DecodeIntegerStream(dictionaryStreams[2]);
            var corpus = FsstExpand(symbolLengths, dictionaryStreams[1].Payload, dictionaryStreams[3].Payload);
            dictionary = SplitBlob(corpus, lengths);
        }
        else
        {
            throw new InvalidDataException($"Unsupported MLT shared dictionary with {dictionaryStreams.Count} streams");
        }

        var columns = new List<AttributeColumn>(meta.Children.Count);
        foreach (var child in meta.Children)
        {
            var childStreamCount = (long)reader.Varint();

            bool[]? present = null;
            if (child.Type % 2 == 1)
            {
                present = StreamDecoder.DecodeBooleanStream(MltStream.Read(reader, boolean: true));
            }

            var offsets = MltStream.Read(reader);
            var codes = StreamDecoder.DecodeIntegerStream(offsets);
            var values = new string?[codes.Length];
            for (var i = 0; i < codes.Length; i++)
            {
                var code = codes[i];
                if (code >= 0 && code < dictionary.Length)
                {
                    values[i] = dictionary[code];
                }
            }

            columns.Add(new AttributeColumn(child.Name!, present, values));
        }

        return columns;
    }

    /// <summary>
    /// Expands an FSST compressed corpus: the symbols of the symbol table, a symbol index or
    /// the escape byte 255 followed by a literal byte per corpus position.
    /// </summary>
    private static byte[] FsstExpand(long[] symbolLengths, byte[] symbolTable, byte[] corpus)
    {
        var symbols = new List<byte[]>(symbolLengths.Length);
        var position = 0;
        foreach (var symbolLength in symbolLengths)
        {
            var length = (int)symbolLength;
            if (position + length > symbolTable.Length)
                throw new InvalidDataException("FSST symbol table exhausted");
            symbols.Add(symbolTable[position..(position + length)]);
            position += length;
        }

        using var output = new MemoryStream(corpus.Length);
        var index = 0;
        while (index < corpus.Length)
        {
            var current = corpus[index++];
            if (current == 255)
            {
                if (index >= corpus.Length)
                    throw new InvalidDataException("FSST corpus ends on an escape byte");
                output.WriteByte(corpus[index++]);
            }
            else
            {
                if (current >= symbols.Count)
                    throw new InvalidDataException($"FSST symbol index {current} outside the symbol table");
                output.Write(symbols[current]);
            }
        }

        return output.ToArray();
    }

    /// <summary>
    /// Splits a blob of UTF-8 strings at the given byte lengths.
    /// </summary>
    private static string[] SplitBlob(byte[] blob, long[] lengths)
    {
        var strings = new string[lengths.Length];
        var position = 0;
        for (var i = 0; i < lengths.Length; i++)
        {
            var length = (int)lengths[i];
            if (position + length > blob.Length)
                throw new InvalidDataException("MLT string blob ends before all strings are read");
            strings[i] = System.Text.Encoding.UTF8.GetString(blob, position, length);
            position += length;
        }

        if (position != blob.Length)
            throw new InvalidDataException("MLT string blob has bytes left after the last string");
        return strings;
    }
}
