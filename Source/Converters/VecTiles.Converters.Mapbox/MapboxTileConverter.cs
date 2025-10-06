using System.IO.Compression;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;

namespace VecTiles.Converters.Mapbox;

/// <summary>
/// Converts a binary array in Mapbox format to a vector tile
/// </summary>
/// <remarks>
/// This converter doesn't read any data from any source. It converts only a already loaded binary
/// array in Mapbox format to a VectorTile.
/// </remarks>
public class MapboxTileConverter : ITileConverter
{
    /// <summary>
    /// Convert a binary array in Mapbox format to a VectorTile
    /// </summary>
    /// <remarks>
    /// If the requested tile couldn't be found, then a parent tile could be used.
    /// In this case, requestedTile and providedTile differ and providedTile is a
    /// parent of requestedTile. This function then extract the part of the providedTile,
    /// that contains all lines, polygons and symbols, that are needed for requestedTile.
    /// Lines and polygons aren't reduced. The have the original size.
    /// </remarks>
    /// <param name="requestedTile">Tile to get</param>
    /// <param name="providedTile">Tile, which data is provided as data</param>
    /// <param name="data">Binary array with the data of providedTile</param>
    /// <returns>Converted data as VectorTile</returns>
    public Task<VectorTile?> Convert(Tile requestedTile, Tile providedTile, byte[] data)
    {
        Stream stream = new MemoryStream(data);

        if (IsGZipped(data))
        {
            stream = new GZipStream(stream, CompressionMode.Decompress);
        }

        return Task.FromResult<VectorTile?>(Parser.Parser.Parse(stream, requestedTile, providedTile));
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