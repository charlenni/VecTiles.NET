using System.IO.Compression;
using VecTiles.Common.Enums;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;

namespace VecTiles.Converters.OpenMapTiles;

/// <summary>
/// Converts a binary array in OpenMapTiles format to a vector tile
/// </summary>
/// <remarks>
/// This converter doesn't read any data from any source. It converts only an already loaded binary
/// array in OpenMapTiles format to a VectorTile.
/// </remarks>
public class OMTTileConverter : ITileConverter
{
    /// <summary>
    /// Convert a glyph collection in Mapbox SDF format
    /// </summary>
    /// <param name="requestedTile">The tile, that should be converted</param>
    /// <param name="providedTile">The tile, that best fit for requested tile</param>
    /// <param name="scheme">Scheme of provided tile</param>
    /// <param name="data">Binary array with the data of providedTile</param>
    /// <returns>Converted data as VectorTile</returns>
    public Task<VectorTile?> Convert(Tile requestedTile, Tile providedTile, Scheme scheme, byte[] data)
    {
        Stream stream = new MemoryStream(data);

        if (IsGZipped(data))
        {
            stream = new GZipStream(stream, CompressionMode.Decompress);
        }

        return Task.FromResult<VectorTile?>(Parser.Parser.Parse(stream, requestedTile, providedTile, scheme));
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