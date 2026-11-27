namespace VecTiles.DataSources.PmTiles;

/// <summary>
/// A directory entry of a PMTiles archive: either the location of one or a run of tiles in the
/// tile data section (<see cref="RunLength"/> greater than 0), or of a leaf directory in the
/// leaf directories section (<see cref="RunLength"/> equal to 0)
/// (see https://github.com/protomaps/PMTiles/blob/main/spec/v3/spec.md, chapter 4).
/// </summary>
public readonly record struct PmTilesEntry
{
    /// <summary>
    /// Tile id of the tile or the first tile of a run, or the first tile id covered by a leaf directory
    /// </summary>
    public required ulong TileId { get; init; }

    /// <summary>
    /// Offset of the tile in the tile data section or of the leaf directory in the leaf directories section
    /// </summary>
    public required ulong Offset { get; init; }

    /// <summary>
    /// Length of the tile or leaf directory in bytes
    /// </summary>
    public required int Length { get; init; }

    /// <summary>
    /// Number of tiles this entry covers; 0 for a leaf directory entry
    /// </summary>
    public required int RunLength { get; init; }
}

/// <summary>
/// Decodes the compressed directories of a PMTiles archive into lists of <see cref="PmTilesEntry"/>.
/// </summary>
public static class PmTilesDirectory
{
    /// <summary>
    /// Decodes a directory: a little endian variable width integer with the number of entries,
    /// followed by four blocks of variable width integers with the (delta encoded) tile ids,
    /// the run lengths, the lengths and the offsets of the entries.
    /// </summary>
    /// <param name="data">The decompressed directory bytes</param>
    /// <returns>The entries of the directory, ascending by tile id</returns>
    /// <exception cref="InvalidDataException">Thrown when the directory data is corrupt</exception>
    public static PmTilesEntry[] Deserialize(byte[] data)
    {
        var position = 0;
        var count = (int)ReadVarint(data, ref position);
        if (count < 0 || position + 4L * count > data.Length)
            throw new InvalidDataException($"Corrupt PMTiles directory: {count} entries do not fit into {data.Length} bytes");

        var tileIds = new ulong[count];
        ulong lastTileId = 0;
        for (var i = 0; i < count; i++)
            tileIds[i] = lastTileId += ReadVarint(data, ref position);

        var runLengths = new int[count];
        for (var i = 0; i < count; i++)
            runLengths[i] = checked((int)ReadVarint(data, ref position));

        var lengths = new int[count];
        for (var i = 0; i < count; i++)
            lengths[i] = checked((int)ReadVarint(data, ref position));

        var entries = new PmTilesEntry[count];
        for (var i = 0; i < count; i++)
        {
            var value = ReadVarint(data, ref position);

            // An offset of 0 encodes contiguity with the previous entry; a 0 offset of the
            // first entry decodes to offset -1 + 1 = 0
            var offset = value == 0 && i > 0
                ? entries[i - 1].Offset + (ulong)entries[i - 1].Length
                : value - 1;

            entries[i] = new PmTilesEntry
            {
                TileId = tileIds[i],
                Offset = offset,
                Length = lengths[i],
                RunLength = runLengths[i]
            };
        }

        return entries;
    }

    /// <summary>
    /// Searches the entry that covers the given tile id: the entry with the greatest tile id
    /// less than or equal to the wanted one, whose range up to the next entry's tile id
    /// contains the wanted one.
    /// </summary>
    /// <param name="entries">The entries of a directory, ascending by tile id</param>
    /// <param name="tileId">The tile id to search</param>
    /// <returns>The covering entry, or null when the directory does not cover the tile id</returns>
    public static PmTilesEntry? Find(PmTilesEntry[] entries, ulong tileId)
    {
        var low = 0;
        var high = entries.Length - 1;
        while (low <= high)
        {
            var middle = low + ((high - low) >> 1);
            if (entries[middle].TileId > tileId)
                high = middle - 1;
            else
                low = middle + 1;
        }

        var index = low - 1;
        if (index < 0)
            return null;

        var entry = entries[index];
        ulong endId = entry.RunLength > 0
            ? entry.TileId + (ulong)entry.RunLength
            : index + 1 < entries.Length ? entries[index + 1].TileId : ulong.MaxValue;

        return tileId < endId ? entry : null;
    }

    /// <summary>
    /// Reads a little endian variable width integer at the current position.
    /// </summary>
    /// <param name="data">Byte array with the value</param>
    /// <param name="position">Position of the value; points to the next byte after the call</param>
    /// <returns>The decoded value</returns>
    /// <exception cref="InvalidDataException">Thrown when the data ends within the value</exception>
    private static ulong ReadVarint(byte[] data, ref int position)
    {
        ulong value = 0;
        var shift = 0;
        while (true)
        {
            if (position >= data.Length)
                throw new InvalidDataException("Unexpected end of data while reading a variable width integer");
            var currentByte = data[position++];
            value |= (ulong)(currentByte & 0x7f) << shift;
            if ((currentByte & 0x80) == 0)
                return value;
            shift += 7;
            if (shift > 63)
                throw new InvalidDataException("Variable width integer exceeds 64 bit");
        }
    }
}
