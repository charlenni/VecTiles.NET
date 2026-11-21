namespace VecTiles.Common.Primitives;

/// <summary>
/// Immutable key representing a set of tile IDs and a rotation value.
/// Used for order-independent comparison of tile collections.
/// </summary>
public readonly struct TilesKey : IEquatable<TilesKey>
{
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
        Rotation = rotation;
    }

    private readonly ulong[] _indices;

    /// <summary>
    /// The rotation step value associated with this key.
    /// </summary>
    public int Rotation { get; }

    /// <summary>
    /// Checks if the given tile ID is contained in this key.
    /// </summary>
    /// <param name="id">The tile ID to check.</param>
    /// <returns><see langword="true"/> if the ID is in this key; otherwise, <see langword="false"/>.</returns>
    public bool Contains(ulong id) =>
        Array.BinarySearch(_indices, id) >= 0;

    /// <summary>
    /// Checks if this key is equal to another key.
    /// Two keys are equal if they have the same rotation and the same sorted tile IDs.
    /// </summary>
    /// <param name="other">The other key to compare with.</param>
    /// <returns><see langword="true"/> if the keys are equal; otherwise, <see langword="false"/>.</returns>
    public bool Equals(TilesKey other)
    {
        if (Rotation != other.Rotation)
            return false;

        if (ReferenceEquals(_indices, other._indices))
            return true;

        if (_indices is null || other._indices is null)
            return false;

        return _indices.AsSpan().SequenceEqual(other._indices);
    }

    /// <summary>
    /// Checks if this key is equal to another object.
    /// </summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns><see langword="true"/> if the keys are equal; otherwise, <see langword="false"/>.</returns>
    public override bool Equals(object? obj) => obj is TilesKey other && Equals(other);

    /// <summary>
    /// Computes the hash code for this key.
    /// </summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Rotation);

        if (_indices is null)
        {
            return hash.ToHashCode();
        }

        foreach (var index in _indices)
        {
            hash.Add(index);
        }

        return hash.ToHashCode();
    }

    /// <summary>
    /// Returns a string representation of this key.
    /// </summary>
    /// <returns>A string containing the rotation and tile count.</returns>
    public override string ToString() =>
        $"TilesKey(Rotation={Rotation}, Count={_indices.Length})";

    /// <summary>
    /// Compares two keys for equality.
    /// </summary>
    /// <param name="left">The left key.</param>
    /// <param name="right">The right key.</param>
    /// <returns><see langword="true"/> if the keys are equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(TilesKey left, TilesKey right) => left.Equals(right);

    /// <summary>
    /// Compares two keys for inequality.
    /// </summary>
    /// <param name="left">The left key.</param>
    /// <param name="right">The right key.</param>
    /// <returns><see langword="true"/> if the keys are unequal; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(TilesKey left, TilesKey right) => !left.Equals(right);
}
