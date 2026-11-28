namespace VecTiles.Converters.MLT.Parser;

/// <summary>
/// Decodes the payload of an MLT stream: the physical encoding turns the bytes into
/// unsigned words, the logical encoding turns the words into the values
/// (see https://maplibre.org/maplibre-tile-spec/encodings/).
/// </summary>
internal static class StreamDecoder
{
    // Physical encodings (bits 1-0 of the encoding byte)
    private const int PhysicalNone = 0;
    private const int PhysicalFastPFOR = 1;
    private const int PhysicalVarInt = 2;
    private const int PhysicalBitPacked = 3;

    // Logical encodings (combinations of bits 7-5 and 4-2 of the encoding byte)
    private const int LogicalNone = 0x00;
    private const int LogicalDelta = 0x20;
    private const int LogicalDeltaRle = 0x2C;
    private const int LogicalComponentwiseDelta = 0x40;
    private const int LogicalRle = 0x60;
    private const int LogicalMortonDelta = 0x84;

    /// <summary>
    /// Decodes an integer stream into its logical values.
    /// </summary>
    /// <param name="stream">The stream to decode</param>
    /// <param name="signed">True when the values of the stream are signed; signed plain and
    /// run length encoded values are ZigZag encoded, deltas are always</param>
    /// <param name="wordBytes">Width of one word in bytes (4 or 8)</param>
    /// <returns>The decoded values</returns>
    internal static long[] DecodeIntegerStream(MltStream stream, bool signed = false, int wordBytes = 4)
    {
        var logical = stream.Encoding & 0xFC;
        switch (logical)
        {
            case LogicalRle:
            {
                var words = DecodeWords(stream, wordBytes);
                var values = ExpandRuns(stream, words);
                if (signed)
                    values = [.. values.Select(v => MltReader.ZigZagDecode((ulong)v))];
                return values;
            }

            case LogicalDeltaRle:
            {
                var words = DecodeWords(stream, wordBytes);
                var deltas = ExpandRuns(stream, words).Select(v => MltReader.ZigZagDecode((ulong)v));
                return PrefixSum(deltas);
            }

            case LogicalDelta:
            {
                var words = DecodeWords(stream, wordBytes);
                return PrefixSum(words.Select(w => MltReader.ZigZagDecode(w)));
            }

            case LogicalComponentwiseDelta:
            {
                // The words are interleaved (x, y) coordinate deltas; the x and y
                // predecessors are separate and both start at 0
                var words = DecodeWords(stream, wordBytes).Select(MltReader.ZigZagDecode).ToArray();
                var values = new long[words.Length];
                long x = 0;
                long y = 0;
                for (var i = 0; i < words.Length; i++)
                {
                    if ((i & 1) == 0)
                    {
                        x += words[i];
                        values[i] = x;
                    }
                    else
                    {
                        y += words[i];
                        values[i] = y;
                    }
                }

                return values;
            }

            case LogicalMortonDelta:
            {
                // The words are plain deltas between the Morton codes of a sorted
                // vertex dictionary: the codes are sorted, so the deltas are never
                // negative and are not ZigZag encoded (the reference decoder only
                // prefix sums them, see fastInverseDelta). Bits and shift come from
                // the stream header
                var words = DecodeWords(stream, wordBytes);
                var bits = stream.Bits ?? throw new InvalidDataException("Morton encoded vertex stream without bits");
                var shift = stream.Shift ?? throw new InvalidDataException("Morton encoded vertex stream without shift");
                var values = new long[words.Length * 2];
                ulong code = 0;
                for (var i = 0; i < words.Length; i++)
                {
                    code += words[i];
                    var (x, y) = MltReader.MortonDecode(code, bits, shift);
                    values[2 * i] = x;
                    values[2 * i + 1] = y;
                }

                return values;
            }

            case LogicalNone:
            {
                var words = DecodeWords(stream, wordBytes);
                if (signed)
                    return [.. words.Select(MltReader.ZigZagDecode)];
                return [.. words.Select(w => (long)w)];
            }

            default:
                throw new InvalidDataException($"Unsupported logical encoding {stream.Encoding:X2} of an MLT stream");
        }
    }

    /// <summary>
    /// Decodes a boolean stream (a Present stream or the data stream of a Bool column):
    /// an ORC style run length encoded bitmap.
    /// </summary>
    /// <param name="stream">The stream to decode</param>
    /// <returns>One bit per value</returns>
    internal static bool[] DecodeBooleanStream(MltStream stream)
    {
        try
        {
            return MltReader.DecodeBooleanRle(stream.Payload, stream.NumValues);
        }
        catch (InvalidDataException)
        {
            // Tolerance for writers that store the bitmap uncompressed
            if (stream.Payload.Length >= (stream.NumValues + 7) / 8)
                return MltReader.BitmapToBits(stream.Payload, stream.NumValues);
            throw;
        }
    }

    /// <summary>
    /// Decodes a float stream: IEEE 754 words, little endian, the only float encoding of MLT v1.
    /// </summary>
    /// <param name="stream">The stream to decode</param>
    /// <param name="doublePrecision">True for 8 byte double words, false for 4 byte floats</param>
    /// <returns>The decoded values</returns>
    internal static double[] DecodeFloatStream(MltStream stream, bool doublePrecision)
    {
        if (doublePrecision)
        {
            var doubles = new double[stream.NumValues];
            for (var i = 0; i < stream.NumValues; i++)
                doubles[i] = BitConverter.Int64BitsToDouble(BitConverter.ToInt64(stream.Payload, i * 8));
            return doubles;
        }

        var floats = new float[stream.NumValues];
        for (var i = 0; i < stream.NumValues; i++)
            floats[i] = BitConverter.UInt32BitsToSingle(BitConverter.ToUInt32(stream.Payload, i * 4));
        return [.. floats.Select(f => (double)f)];
    }

    /// <summary>
    /// Decodes the words of a stream with its physical encoding.
    /// </summary>
    /// <param name="stream">The stream to decode</param>
    /// <param name="wordBytes">Width of one word in bytes (4 or 8)</param>
    /// <returns>The decoded unsigned words</returns>
    private static ulong[] DecodeWords(MltStream stream, int wordBytes)
    {
        var physical = stream.Encoding & 0x03;
        return physical switch
        {
            PhysicalVarInt => DecodeVarInt(stream.Payload, stream.NumValues),
            PhysicalNone => DecodeFixed(stream.Payload, stream.NumValues, wordBytes),
            PhysicalBitPacked => DecodeBitPacked(stream.Payload, stream.NumValues),
            PhysicalFastPFOR => FastPFOR.Decode(stream.Payload, stream.NumValues),
            _ => throw new InvalidDataException($"Unsupported physical encoding {physical} of an MLT stream"),
        };
    }

    /// <summary>
    /// Expands the runs of an RLE stream: the words are all run lengths, then all values.
    /// </summary>
    /// <param name="stream">The stream to expand</param>
    /// <param name="words">The physically decoded words</param>
    /// <returns>The expanded values</returns>
    private static long[] ExpandRuns(MltStream stream, ulong[] words)
    {
        var runs = (int)(stream.Runs ?? throw new InvalidDataException("RLE stream without run count"));
        var expected = stream.NumRleValues ?? throw new InvalidDataException("RLE stream without expanded value count");
        if (words.Length < 2 * runs)
            throw new InvalidDataException($"RLE stream holds {words.Length} words for {runs} runs");

        var values = new List<long>((int)expected);
        for (var i = 0; i < runs; i++)
        {
            var runLength = (long)words[i];
            var value = (long)words[runs + i];
            for (var j = 0; j < runLength; j++)
                values.Add(value);
        }

        if (values.Count != expected)
            throw new InvalidDataException($"RLE stream expands to {values.Count} values, {expected} expected");
        return [.. values];
    }

    /// <summary>
    /// Sums up ZigZag decoded deltas to values; the first predecessor is 0.
    /// </summary>
    private static long[] PrefixSum(IEnumerable<long> deltas)
    {
        var values = new List<long>();
        long sum = 0;
        foreach (var delta in deltas)
        {
            sum += delta;
            values.Add(sum);
        }

        return [.. values];
    }

    /// <summary>
    /// Reads unsigned variable width integers.
    /// </summary>
    private static ulong[] DecodeVarInt(byte[] payload, long count)
    {
        var values = new ulong[count];
        var position = 0;
        for (var i = 0; i < count; i++)
        {
            ulong value = 0;
            var shift = 0;
            while (true)
            {
                if (position >= payload.Length)
                    throw new InvalidDataException("Variable width integers end before the value count is reached");
                var current = payload[position++];
                value |= (ulong)(current & 0x7f) << shift;
                if ((current & 0x80) == 0)
                    break;
                shift += 7;
                if (shift > 63)
                    throw new InvalidDataException("Variable width integer exceeds 64 bit");
            }

            values[i] = value;
        }

        return values;
    }

    /// <summary>
    /// Reads fixed width little endian words.
    /// </summary>
    private static ulong[] DecodeFixed(byte[] payload, long count, int wordBytes)
    {
        if (payload.Length < count * wordBytes)
            throw new InvalidDataException($"Fixed width stream holds {payload.Length} bytes for {count} words of {wordBytes} bytes");
        var values = new ulong[count];
        for (var i = 0; i < count; i++)
        {
            values[i] = wordBytes == 4
                ? System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(i * 4))
                : System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(payload.AsSpan(i * 8));
        }

        return values;
    }

    /// <summary>
    /// Reads bit packed words: a width byte, then all values LSB first at the same
    /// number of bits.
    /// </summary>
    private static ulong[] DecodeBitPacked(byte[] payload, long count)
    {
        if (payload.Length < 1)
            throw new InvalidDataException("Bit packed stream without width byte");
        var width = payload[0];
        if (width == 0 || width > 32)
            throw new InvalidDataException($"Bit packed stream with invalid width {width}");
        if (payload.Length < 1 + (count * width + 7) / 8)
            throw new InvalidDataException($"Bit packed stream holds {payload.Length} bytes for {count} values of {width} bits");

        var values = new ulong[count];
        var bitPosition = 8;
        for (var i = 0; i < count; i++)
        {
            ulong value = 0;
            for (var bit = 0; bit < width; bit++)
            {
                var currentBit = payload[bitPosition >> 3] >> (bitPosition & 7) & 1;
                value |= (ulong)currentBit << bit;
                bitPosition++;
            }

            values[i] = value;
        }

        return values;
    }
}
