using System.IO.Compression;
using ProtoBuf;
using VecTiles.Converters.SdfFont.Pbf;

namespace VecTiles.Converters.SdfFont;

public class SdfFontConverter
{
    /// <summary>
    /// Convert a glyph collection in Mapbox SDF format
    /// </summary>
    /// <param name="data">Binary array with the data of fontstack/range</param>
    /// <returns>Converted data as VectorTile</returns>
    public Task<PbfGlyphs?> Convert(byte[] data)
    {
        Stream stream = new MemoryStream(data);

        if (IsGZipped(data))
        {
            stream = new GZipStream(stream, CompressionMode.Decompress);
        }

        return Task.FromResult<PbfGlyphs?>(Parse(stream));
    }

    private static PbfGlyphs Parse(Stream stream)
    {
        return Serializer.Deserialize<PbfGlyphs>(stream);
    }

    /// <summary>
    /// Check, if byte data is zipped in GZip format
    /// </summary>
    /// <param name="data">Byte array with data</param>
    /// <returns>True, if binaray data is zipped in GZip format</returns>
    private static bool IsGZipped(byte[] data)
    {
        return IsZipped(data, 3, "1F-8B-08");
    }

    /// <summary>
    /// Check, if byte data is zipped
    /// </summary>
    /// <param name="data">Byte array with data</param>
    /// <param name="signatureSize">Length of signatur to check</param>
    /// <param name="expectedSignature">Expected signature for format</param>
    /// <returns>True, if binaray data is zipped</returns>
    private static bool IsZipped(byte[] data, int signatureSize = 4, string expectedSignature = "50-4B-03-04")
    {
        if (data.Length < signatureSize)
            return false;
        byte[] signature = new byte[signatureSize];
        Buffer.BlockCopy(data, 0, signature, 0, signatureSize);
        string actualSignature = BitConverter.ToString(signature);
        return actualSignature == expectedSignature;
    }
}
