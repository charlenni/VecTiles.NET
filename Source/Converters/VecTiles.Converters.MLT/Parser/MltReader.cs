using System.Buffers.Binary;
using System.Text;

namespace VecTiles.Converters.MLT.Parser;

/// <summary>
/// Byte reader for the low level elements of an MLT tile: variable width integers,
/// length prefixed strings and the ORC style boolean run length encoding of the
/// Present and Bool streams (see https://maplibre.org/maplibre-tile-spec/).
/// </summary>
internal sealed class MltReader(byte[] data, int position = 0)
{
    private readonly byte[] _data = data;

    /// <summary>
    /// Gets or sets the current read position.
    /// </summary>
    internal int Position { get; set; } = position;

    /// <summary>
    /// Reads a single byte.
    /// </summary>
    internal byte Byte()
    {
        if (Position >= _data.Length)
            throw new InvalidDataException("Unexpected end of MLT data while reading a byte");
        return _data[Position++];
    }

    /// <summary>
    /// Reads an unsigned variable width integer (LEB128, 7 bits per byte, least
    /// significant group first).
    /// </summary>
    internal ulong Varint()
    {
        ulong value = 0;
        var shift = 0;
        while (true)
        {
            var current = Byte();
            value |= (ulong)(current & 0x7f) << shift;
            if ((current & 0x80) == 0)
                return value;
            shift += 7;
            if (shift > 63)
                throw new InvalidDataException("Variable width integer exceeds 64 bit");
        }
    }

    /// <summary>
    /// Reads a length prefixed UTF-8 string.
    /// </summary>
    internal string String()
    {
        var length = (int)Varint();
        return Encoding.UTF8.GetString(Bytes(length));
    }

    /// <summary>
    /// Reads the given number of bytes.
    /// </summary>
    internal byte[] Bytes(long count)
    {
        if (Position + count > _data.Length)
            throw new InvalidDataException($"Unexpected end of MLT data: {count} bytes requested, {_data.Length - Position} left");
        var result = _data[Position..(int)(Position + count)];
        Position += (int)count;
        return result;
    }

    /// <summary>
    /// Decodes a ZigZag encoded signed value.
    /// </summary>
    internal static long ZigZagDecode(ulong value)
    {
        return (long)(value >> 1) ^ -(long)(value & 1);
    }

    /// <summary>
    /// Decodes the ORC style run length encoding of a boolean stream: a control byte below
    /// 128 starts a repeated run (the next byte, control + 3 times), a control byte of 128
    /// or more a literal run of 256 - control bytes. The runs expand to the bitmap bytes of
    /// the stream.
    /// </summary>
    internal static bool[] DecodeBooleanRle(byte[] payload, long count)
    {
        var bitmapBytes = (int)((count + 7) / 8);
        var bitmap = new byte[bitmapBytes];
        var position = 0;
        var written = 0;
        while (written < bitmapBytes)
        {
            if (position >= payload.Length)
                throw new InvalidDataException("Boolean run length encoding ends before the bitmap is complete");
            var control = payload[position++];
            if (control < 128)
            {
                if (position >= payload.Length)
                    throw new InvalidDataException("Boolean run length encoding ends within a repeated run");
                var value = payload[position++];
                var runLength = control + 3;
                for (var i = 0; i < runLength && written < bitmapBytes; i++)
                    bitmap[written++] = value;
            }
            else
            {
                var runLength = 256 - control;
                if (position + runLength > payload.Length)
                    throw new InvalidDataException("Boolean run length encoding ends within a literal run");
                for (var i = 0; i < runLength && written < bitmapBytes; i++)
                    bitmap[written++] = payload[position++];
            }
        }

        return BitmapToBits(bitmap, count);
    }

    /// <summary>
    /// Expands a bitmap to single bits, least significant bit of each byte first.
    /// </summary>
    internal static bool[] BitmapToBits(byte[] bitmap, long count)
    {
        var bits = new bool[count];
        for (long i = 0; i < count; i++)
            bits[i] = (bitmap[i >> 3] >> (int)(i & 7) & 1) != 0;
        return bits;
    }

    /// <summary>
    /// Decodes a Morton (Z-order) code into a coordinate pair, undoing the coordinate shift.
    /// </summary>
    internal static (long X, long Y) MortonDecode(ulong code, long bits, long shift)
    {
        long x = 0;
        long y = 0;
        for (var i = 0; i < bits; i++)
        {
            var mask = 1ul << (int)(2 * i);
            x |= (long)((code & mask) >> (int)i);
            y |= (long)(((code >> 1) & mask) >> (int)i);
        }

        return (x - shift, y - shift);
    }

    /// <summary>
    /// Reads a 32 bit big endian unsigned integer.
    /// </summary>
    internal static uint ReadUInt32BigEndian(byte[] data, int position)
    {
        return BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(position));
    }
}
