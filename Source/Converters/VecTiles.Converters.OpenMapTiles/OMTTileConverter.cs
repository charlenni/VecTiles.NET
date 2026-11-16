using System.Buffers;
using System.Buffers.Binary;
using System.IO.Compression;
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
    public Task<VectorTile?> Convert(Tile requestedTile, Tile providedTile, byte[] data)
    {
        if (!IsGZipped(data))
        {
            return Task.FromResult<VectorTile?>(Parser.Parser.Parse(data, requestedTile, providedTile));
        }

        // Read uncompressed length from last 4 bytes
        uint uncompressedSize = BinaryPrimitives.ReadUInt32LittleEndian(
            data.AsSpan(data.Length - 4));
        // Safety fallback, if size is not plausible (eg. > 16 MB)
        int initialCapacity = (uncompressedSize > 0 && uncompressedSize < 16 * 1024 * 1024)
            ? (int)uncompressedSize
            : data.Length * 4;
        // Get buffer from pool leihen (zero heap allocation!)
        byte[] rentedBuffer = ArrayPool<byte>.Shared.Rent(initialCapacity);
        try
        {
            // Input stream without data copy over byte[]
            using var inputStream = new MemoryStream(data, writable: false);
            using var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress);

            int totalBytesRead = 0;
            while (true)
            {
                // If buffer was to small, resize it
                if (totalBytesRead == rentedBuffer.Length)
                {
                    byte[] biggerBuffer = ArrayPool<byte>.Shared.Rent(rentedBuffer.Length * 2);
                    Buffer.BlockCopy(rentedBuffer, 0, biggerBuffer, 0, totalBytesRead);
                    ArrayPool<byte>.Shared.Return(rentedBuffer);
                    rentedBuffer = biggerBuffer;
                }

                int bytesRead = gzipStream.Read(rentedBuffer.AsSpan(totalBytesRead));
                if (bytesRead == 0)
                {
                    break;
                }

                totalBytesRead += bytesRead;
            }

            // 4. Direkt vom Span deserialisieren – kein zweiter MemoryStream nötig!
            var decompressedSpan = rentedBuffer.AsMemory(0, totalBytesRead);
            return Task.FromResult<VectorTile?>(Parser.Parser.Parse(decompressedSpan, requestedTile, providedTile));
        }
        finally
        {
            // Finally return buffer to pool
            ArrayPool<byte>.Shared.Return(rentedBuffer);
        }
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
