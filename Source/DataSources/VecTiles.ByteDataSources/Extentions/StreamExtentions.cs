// Copyright (c) The Mapsui authors.
// The Mapsui authors licensed this file under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace VecTiles.DataSources.Extensions;

/// <summary>
/// Extensions for class Stream.
/// </summary>
public static class StreamExtensions
{
    /// <summary>
    /// Convert stream to byte array
    /// </summary>
    /// <param name="input">Stream to convert</param>
    /// <returns>Byte array with data from stream</returns>
    public static byte[] ToBytes(this Stream input)
    {
        using var ms = new MemoryStream();

        switch (input.GetType().Name)
        {
            case "ContentLengthReadStream":
            case "ReadOnlyStream":
                // Not implemented
                break;
            default:
                if (input.Position != 0)
                {
                    // Set position to 0 so that i can copy all the data
                    input.Position = 0;
                }

                break;
        }

        input.CopyTo(ms);
        return ms.ToArray();
    }
}
