namespace VecTiles.Converters.Mapbox.Parser;

/// <summary>
/// Provides methods for ZigZag encoding and decoding of signed integers.
/// </summary>
public static class ZigZag
{
    /// <summary>
    /// Decodes a ZigZag-encoded integer.
    /// </summary>
    /// <param name="n">The ZigZag-encoded value.</param>
    /// <returns>The decoded signed integer.</returns>
    public static long Decode(long n)
    {
        return (n >> 1) ^ (-(n & 1));
    }

    /// <summary>
    /// Encodes a signed integer using ZigZag encoding.
    /// </summary>
    /// <param name="n">The signed integer to encode.</param>
    /// <returns>The ZigZag-encoded value.</returns>
    public static long Encode(long n)
    {
        return (n << 1) ^ (n >> 31);
    }
}
