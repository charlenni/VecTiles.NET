namespace VecTiles.Converters.MLT.Parser;

/// <summary>
/// The header and payload of one MLT stream. A logical column is split into streams
/// (sub-columns); every stream is a header followed by its payload
/// (see https://maplibre.org/maplibre-tile-spec/specification/v1/, chapter streams).
/// </summary>
internal sealed class MltStream
{
    /// <summary>
    /// Category (bits 7-4) and subtype (bits 3-0) of the stream.
    /// </summary>
    internal byte StreamType { get; init; }

    /// <summary>
    /// Logical (bits 7-5 and 4-2) and physical (bits 1-0) encoding of the payload.
    /// </summary>
    internal byte Encoding { get; init; }

    /// <summary>
    /// Number of values the stream holds. On RLE streams the number of physically
    /// encoded words (run lengths and values), on all other streams the value count.
    /// </summary>
    internal long NumValues { get; init; }

    /// <summary>
    /// Size of the payload in bytes.
    /// </summary>
    internal long ByteLength { get; init; }

    /// <summary>
    /// Number of runs of an RLE stream; null on all other streams. Boolean streams
    /// (Present and Bool) carry no run count.
    /// </summary>
    internal long? Runs { get; init; }

    /// <summary>
    /// Number of values of an RLE stream after run expansion; null on all other streams.
    /// </summary>
    internal long? NumRleValues { get; init; }

    /// <summary>
    /// Bits per axis of a Morton encoded vertex stream.
    /// </summary>
    internal long? Bits { get; init; }

    /// <summary>
    /// Coordinate shift of a Morton encoded vertex stream.
    /// </summary>
    internal long? Shift { get; init; }

    /// <summary>
    /// The payload bytes.
    /// </summary>
    internal required byte[] Payload { get; init; }

    /// <summary>
    /// Gets the category of the stream (high nibble of the stream type).
    /// </summary>
    internal byte Category => (byte)(StreamType >> 4);

    /// <summary>
    /// Gets the subtype of the stream (low nibble of the stream type).
    /// </summary>
    internal byte Subtype => (byte)(StreamType & 0xF);

    /// <summary>
    /// Reads a stream header and payload from the given position.
    /// </summary>
    /// <param name="reader">Reader positioned at the stream</param>
    /// <param name="boolean">True for the Present streams and the Bool data streams, which
    /// carry no run count and hold an ORC style run length encoded bitmap</param>
    /// <returns>The parsed stream</returns>
    internal static MltStream Read(MltReader reader, bool boolean = false)
    {
        var streamType = reader.Byte();
        var encoding = reader.Byte();
        var numValues = (long)reader.Varint();
        var byteLength = (long)reader.Varint();

        long? runs = null;
        long? numRleValues = null;
        long? bits = null;
        long? shift = null;

        var logical1 = encoding >> 5;
        var logical2 = (encoding >> 2) & 0x7;
        var isRle = (logical1, logical2) is (3, 0) or (1, 3);
        if (isRle && !boolean)
        {
            runs = (long)reader.Varint();
            numRleValues = (long)reader.Varint();
        }

        if (logical1 == 4 && logical2 == 1)
        {
            bits = (long)reader.Varint();
            shift = (long)reader.Varint();
        }

        var payload = reader.Bytes(byteLength);

        return new MltStream
        {
            StreamType = streamType,
            Encoding = encoding,
            NumValues = numValues,
            ByteLength = byteLength,
            Runs = runs,
            NumRleValues = numRleValues,
            Bits = bits,
            Shift = shift,
            Payload = payload,
        };
    }
}
