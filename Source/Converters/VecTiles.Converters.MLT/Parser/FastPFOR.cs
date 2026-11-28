using System.Collections.Generic;

namespace VecTiles.Converters.MLT.Parser;

/// <summary>
/// Decodes the FastPFOR physical encoding, v1 variant: blocks of 128 words packed
/// sequentially 32 values at a time, big endian words on the wire, exceptions collected
/// per page (see https://maplibre.org/maplibre-tile-spec/encodings/).
/// MLT v1 has no 64 bit FastPFOR, so this decoder works on 32 bit words only.
/// </summary>
internal static class FastPFOR
{
    private const int BlockSize = 128;
    private const int PageSizeValues = 65536;
    private const int MaxWidth = 32;

    /// <summary>
    /// Decodes a FastPFOR payload into its words.
    /// </summary>
    /// <param name="payload">The stream payload</param>
    /// <param name="count">The number of values of the stream</param>
    /// <returns>The decoded unsigned words</returns>
    internal static ulong[] Decode(byte[] payload, long count)
    {
        if (count == 0)
            return [];

        if (payload.Length == 0 || payload.Length % 4 != 0)
            throw new InvalidDataException("FastPFOR payload is empty or not a multiple of 4 bytes");

        var blockValues = (long)ReadUInt32BE(payload, 0);
        if (blockValues % BlockSize != 0 || blockValues > count)
            throw new InvalidDataException($"FastPFOR block value count {blockValues} is invalid for {count} values");

        var values = new ulong[count];
        var written = 0L;
        var position = 4;
        var pages = (blockValues + PageSizeValues - 1) / PageSizeValues;
        for (var page = 0; page < pages; page++)
        {
            var pageValues = (int)Math.Min(PageSizeValues, blockValues - page * PageSizeValues);
            DecodePage(payload, ref position, values, ref written, pageValues);
        }

        // The tail is variable byte encoded, bit 7 of the last byte of a value is set,
        // 0x00 padding never ends a value, an unfinished value at the end is discarded
        ulong accumulator = 0;
        var shift = 0;
        while (position < payload.Length)
        {
            var current = payload[position++];
            accumulator |= (ulong)(current & 0x7f) << shift;
            if ((current & 0x80) != 0)
            {
                if (written >= count)
                    throw new InvalidDataException("FastPFOR tail holds more values than the stream");
                values[written++] = accumulator;
                accumulator = 0;
                shift = 0;
            }
            else
            {
                shift += 7;
            }
        }

        if (written != count)
            throw new InvalidDataException($"FastPFOR decoded {written} values, {count} expected");
        return values;
    }

    /// <summary>
    /// Decodes one page: its packed blocks, block metadata, bitmap and exception arrays.
    /// </summary>
    private static void DecodePage(byte[] payload, ref int position, ulong[] values, ref long written, int pageValues)
    {
        var blocks = pageValues / BlockSize;
        var metaOffset = (int)ReadUInt32BE(payload, position);
        // Words from the meta_offset word to the meta_size word, 1 + the packed words
        var packedWords = metaOffset - 1;
        var packedStart = position + 4;
        var metaSizePosition = packedStart + 4 * packedWords;
        if (packedWords < 0 || metaSizePosition + 4 > payload.Length)
            throw new InvalidDataException("FastPFOR page metadata offset out of range");
        var metaSize = (int)ReadUInt32BE(payload, metaSizePosition);
        var metaPosition = metaSizePosition + 4;
        var metaEnd = metaPosition + metaSize;
        if (metaEnd > payload.Length)
            throw new InvalidDataException("FastPFOR page metadata exceeds the payload");

        // The metadata is one entry per block, in block order
        var widths = new int[blocks];
        var indexes = new int[blocks];
        var positions = new int[blocks][];
        for (var block = 0; block < blocks; block++)
        {
            var width = payload[metaPosition++];
            if (width > MaxWidth)
                throw new InvalidDataException($"FastPFOR block width {width} above the value width");
            widths[block] = width;

            var exceptions = payload[metaPosition++];
            positions[block] = new int[exceptions];
            if (exceptions > 0)
            {
                var maxWidth = payload[metaPosition++];
                // An exception must be strictly wider than the block
                if (maxWidth <= width || maxWidth > MaxWidth)
                    throw new InvalidDataException($"FastPFOR block max width {maxWidth} invalid at width {width}");
                indexes[block] = maxWidth - width;
                for (var e = 0; e < exceptions; e++)
                {
                    var value = payload[metaPosition++];
                    if (value >= BlockSize)
                        throw new InvalidDataException($"FastPFOR exception position {value} outside the block");
                    positions[block][e] = value;
                }
            }
        }

        if (metaPosition > metaEnd)
            throw new InvalidDataException("FastPFOR block metadata exceeds its size");

        // The metadata is zero padded to whole words, then the bitmap follows
        position = (metaEnd + 3) & ~3;
        if (position + 4 > payload.Length)
            throw new InvalidDataException("FastPFOR page ends before its bitmap");
        var bitmap = ReadUInt32BE(payload, position);
        position += 4;

        // An exception array can exist for each width k from 2 to the value width,
        // bit k - 1 of the bitmap says whether it does; arrays follow in ascending k
        var arrays = new Dictionary<int, ulong[]>();
        var cursors = new Dictionary<int, int>();
        for (var k = 2; k <= MaxWidth; k++)
        {
            if (((bitmap >> (k - 1)) & 1) == 0)
                continue;
            var arrayCount = (int)ReadUInt32BE(payload, position);
            position += 4;
            if (arrayCount > PageSizeValues)
                throw new InvalidDataException($"FastPFOR exception array with {arrayCount} values above the page size");
            arrays[k] = ReadExceptionValues(payload, ref position, arrayCount, k);
            cursors[k] = 0;
        }

        // The blocks are packed one after another, each at its own width
        var blockWord = packedStart;
        for (var block = 0; block < blocks; block++)
        {
            var width = widths[block];
            var blockStart = written;
            if (width == 0)
            {
                for (var i = 0; i < BlockSize; i++)
                    values[written++] = 0;
            }
            else
            {
                // 128 values, one continuous LSB-first bit stream of 4 * width words
                for (var i = 0; i < BlockSize; i++)
                    values[written++] = ExtractBits(payload, blockWord, i * width, width);
                blockWord += 16 * width;
            }

            // Patch the exceptions of the block
            foreach (var exceptionPosition in positions[block])
            {
                if (indexes[block] == 1)
                {
                    // A single bit wider than the block: the high part can only be 1
                    values[blockStart + exceptionPosition] |= 1UL << width;
                }
                else
                {
                    if (!arrays.TryGetValue(indexes[block], out var array) || cursors[indexes[block]] >= array.Length)
                        throw new InvalidDataException("FastPFOR exception without a value left in its array");
                    values[blockStart + exceptionPosition] |= array[cursors[indexes[block]]++] << width;
                }
            }
        }

        if (position > payload.Length)
            throw new InvalidDataException("FastPFOR payload ends inside a page");
    }

    /// <summary>
    /// Reads the values of one exception array: groups of 128 interleaved in four lanes,
    /// then the rest in groups of 32 sequentially, all big endian.
    /// </summary>
    private static ulong[] ReadExceptionValues(byte[] payload, ref int position, int count, int k)
    {
        var values = new ulong[count];
        var positionLocal = position;
        var index = 0;

        // Interleaved groups: value i belongs to lane i mod 4 and row i / 4; word w of
        // lane l is the word at 4 * w + l of the group
        while (count - index >= 128)
        {
            if (positionLocal + 16 * k > payload.Length)
                throw new InvalidDataException("FastPFOR payload ends inside an exception array");
            for (var i = 0; i < 128; i++)
            {
                var lane = i % 4;
                var row = i / 4;
                values[index + i] = ExtractLaneBits(payload, positionLocal, lane, row * k, k);
            }

            positionLocal += 16 * k;
            index += 128;
        }

        // The rest in groups of 32 sequentially, one continuous LSB-first bit stream,
        // zero padded to whole words
        var remainder = count - index;
        if (remainder > 0)
        {
            var words = (remainder * k + 31) / 32;
            if (positionLocal + 4 * words > payload.Length)
                throw new InvalidDataException("FastPFOR payload ends inside an exception array");
            for (var i = 0; i < remainder; i++)
                values[index + i] = ExtractBits(payload, positionLocal, i * k, k);
            positionLocal += 4 * words;
        }

        position = positionLocal;
        return values;
    }

    /// <summary>
    /// Reads width bits of a continuous LSB-first bit stream of big endian words.
    /// </summary>
    private static ulong ExtractBits(byte[] payload, int basePosition, int bit, int width)
    {
        var word = ReadUInt32BE(payload, basePosition + 4 * (bit / 32));
        var offset = bit % 32;
        if (offset + width <= 32)
            return (word >> offset) & Mask(width);
        var next = ReadUInt32BE(payload, basePosition + 4 * (bit / 32 + 1));
        return (((ulong)next << 32) | word) >> offset & Mask(width);
    }

    /// <summary>
    /// Reads width bits of one lane of an interleaved group, where word w of the lane
    /// is the word at 4 * w + lane of the group.
    /// </summary>
    private static ulong ExtractLaneBits(byte[] payload, int groupPosition, int lane, int bit, int width)
    {
        var word = ReadUInt32BE(payload, groupPosition + 4 * (4 * (bit / 32) + lane));
        var offset = bit % 32;
        if (offset + width <= 32)
            return (word >> offset) & Mask(width);
        var next = ReadUInt32BE(payload, groupPosition + 4 * (4 * (bit / 32 + 1) + lane));
        return (((ulong)next << 32) | word) >> offset & Mask(width);
    }

    private static ulong Mask(int width) => width >= 64 ? ulong.MaxValue : (1UL << width) - 1;

    private static uint ReadUInt32BE(byte[] payload, int position) =>
        (uint)(payload[position] << 24 | payload[position + 1] << 16 | payload[position + 2] << 8 | payload[position + 3]);
}
