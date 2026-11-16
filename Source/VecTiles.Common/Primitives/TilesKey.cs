namespace VecTiles.Common.Primitives;

/// <summary>
/// Immutable key representing a set of tile IDs and a rotation value.
/// Used for order-independent comparison of tile collections.
/// </summary>
public readonly record struct TilesKey
{
    private readonly ulong[] _indices;

    /// <summary>
    /// Initializes a new key for the given tiles and rotation.
    /// </summary>
    /// <param name="tiles">The collection of tiles to generate the key from.</param>
    /// <param name="rotation">The rotation value (0-255) to include in the key.</param>
    /// <exception cref="ArgumentNullException">Throws if <paramref name="tiles"/> is null.</exception>
    public TilesKey(IEnumerable<Tile> tiles, int rotation = 0)
    {
        ArgumentNullException.ThrowIfNull(tiles);

        var tileArray = tiles.ToArray();
        _indices = new ulong[tileArray.Length];
        for (int i = 0; i < tileArray.Length; i++)
        {
            _indices[i] = tileArray[i].Id;
        }

        // Sorts tile IDs in-place to ensure order independence (Level, Col, Row)
        Array.Sort(_indices);
        Rotation = (byte)rotation;
    }

    /// <summary>
    /// The rotation value associated with this key.
    /// </summary>
    public byte Rotation { get; }

    /// <summary>
    /// Checks if the given tile ID is contained in this key.
    /// </summary>
    /// <param name="id">The tile ID to check.</param>
    /// <returns><see langword="true"/> if the ID is in this key; otherwise, <see langword="false"/>.</returns>
    public bool Contains(ulong id) =>
        Array.BinarySearch(_indices, id) >= 0;

    /// <summary>
    /// Returns a string representation of this key.
    /// </summary>
    /// <returns>A string containing the rotation and tile count.</returns>
    public override string ToString() =>
        $"TilesKey(Rotation={Rotation}, Count={_indices.Length})";
}
