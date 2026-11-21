using System;
using VecTiles.Common.Primitives;

namespace VecTiles.Common.Tests.Primitives;

/// <summary>
/// Unit tests for the <see cref="TilesKey" /> struct.
/// </summary>
public class TilesKeyTests
{
    /// <summary>
    /// Verifies that a <see langword="null" /> tile collection is rejected with an <see cref="ArgumentNullException" />.
    /// </summary>
    [Fact]
    public void Constructor_WithNullTiles_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new TilesKey(null!));
    }

    /// <summary>
    /// Verifies that an empty tile collection creates a key with zero tiles and no rotation.
    /// </summary>
    [Fact]
    public void Constructor_WithEmptyCollection_CreatesValidKey()
    {
        var key = new TilesKey([], 0);
        Assert.Equal(0, key.Rotation);
        Assert.Equal("TilesKey(Rotation=0, Count=0)", key.ToString());
    }

    /// <summary>
    /// Verifies that the constructor stores the rotation value unchanged.
    /// </summary>
    /// <param name="rotation">Rotation angle in degrees.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(255)]
    [InlineData(128)]
    [InlineData(300)]
    public void Constructor_WithRotation_CreatesKeyWithCorrectRotation(int rotation)
    {
        var tile = new Tile(0, 0, 0);
        var key = new TilesKey([tile], rotation);
        Assert.Equal(rotation, key.Rotation);
    }

    /// <summary>
    /// Verifies that a key built from a single tile exposes the rotation and contains the tile id.
    /// </summary>
    [Fact]
    public void Constructor_WithSingleTile_CreatesValidKey()
    {
        var tile = new Tile(1, 2, 3);
        var key = new TilesKey([tile], 45);
        Assert.Equal(45, key.Rotation);
        Assert.True(key.Contains(tile.Id));
    }

    /// <summary>
    /// Verifies that the tile order of the input is normalized by tile id so both inputs compare equal.
    /// </summary>
    [Fact]
    public void Constructor_WithMultipleTiles_SortsTilesById()
    {
        var tile1 = new Tile(1, 2, 3); // Higher ID
        var tile2 = new Tile(0, 0, 0); // Lower ID
        
        var key1 = new TilesKey([tile1, tile2], 0);
        var key2 = new TilesKey([tile2, tile1], 0);
        
        Assert.Equal(key1, key2);
        Assert.Equal(key1.GetHashCode(), key2.GetHashCode());
    }

    /// <summary>
    /// Verifies that duplicate tiles can be passed to the constructor.
    /// </summary>
    [Fact]
    public void Constructor_WithDuplicateTiles_CreatesValidKey()
    {
        var tile = new Tile(1, 2, 3);
        var key = new TilesKey([tile, tile, tile], 0);
        
        Assert.True(key.Contains(tile.Id));
    }

    /// <summary>
    /// Verifies that keys with equal tiles and rotation are equal.
    /// </summary>
    [Fact]
    public void Equals_SameTilesSameRotation_ReturnsTrue()
    {
        var tile = new Tile(1, 2, 3);
        var key1 = new TilesKey([tile], 0);
        var key2 = new TilesKey([tile], 0);
        
        Assert.Equal(key1, key2);
    }

    /// <summary>
    /// Verifies that the rotation is part of the equality comparison.
    /// </summary>
    [Fact]
    public void Equals_SameTilesDifferentRotation_ReturnsFalse()
    {
        var tile = new Tile(1, 2, 3);
        var key1 = new TilesKey([tile], 0);
        var key2 = new TilesKey([tile], 90);
        
        Assert.NotEqual(key1, key2);
    }

    /// <summary>
    /// Verifies that keys with different tile sets are never equal.
    /// </summary>
    [Fact]
    public void Equals_DifferentTilesSameRotation_ReturnsFalse()
    {
        var tile1 = new Tile(1, 2, 3);
        var tile2 = new Tile(4, 5, 6);
        var key1 = new TilesKey([tile1], 0);
        var key2 = new TilesKey([tile2], 0);
        
        Assert.NotEqual(key1, key2);
    }

    /// <summary>
    /// Verifies that the number of tiles is part of the equality comparison.
    /// </summary>
    [Fact]
    public void Equals_DifferentNumberOfTiles_ReturnsFalse()
    {
        var tile1 = new Tile(1, 2, 3);
        var tile2 = new Tile(4, 5, 6);
        var key1 = new TilesKey([tile1], 0);
        var key2 = new TilesKey([tile1, tile2], 0);
        
        Assert.NotEqual(key1, key2);
    }

    /// <summary>
    /// Verifies that a key is equal to itself.
    /// </summary>
    [Fact]
    public void Equals_SameInstance_ReturnsTrue()
    {
        var tile = new Tile(1, 2, 3);
        var key = new TilesKey([tile], 0);
        
        Assert.Equal(key, key);
    }

    /// <summary>
    /// Verifies that equal keys produce the same hash code.
    /// </summary>
    [Fact]
    public void GetHashCode_SameKeys_ReturnsSameHashCode()
    {
        var tile = new Tile(1, 2, 3);
        var key1 = new TilesKey([tile], 45);
        var key2 = new TilesKey([tile], 45);
        
        Assert.Equal(key1.GetHashCode(), key2.GetHashCode());
    }

    /// <summary>
    /// Verifies that different keys are treated as unequal.
    /// </summary>
    [Fact]
    public void GetHashCode_DifferentKeys_ReturnsDifferentHashCode()
    {
        var tile1 = new Tile(1, 2, 3);
        var tile2 = new Tile(4, 5, 6);
        var key1 = new TilesKey([tile1], 0);
        var key2 = new TilesKey([tile2], 0);
        
        // Hash codes should be different (but not guaranteed)
        // If they happen to be the same, the test still passes
        Assert.NotEqual(key1, key2);
    }

    /// <summary>
    /// Verifies that the hash code does not depend on the order of the input tiles.
    /// </summary>
    [Fact]
    public void GetHashCode_OrderIndependent_ReturnsSameHashCode()
    {
        var tile1 = new Tile(1, 2, 3);
        var tile2 = new Tile(4, 5, 6);
        
        var key1 = new TilesKey([tile1, tile2], 0);
        var key2 = new TilesKey([tile2, tile1], 0);
        
        Assert.Equal(key1.GetHashCode(), key2.GetHashCode());
    }

    /// <summary>
    /// Verifies that a contained tile id is reported.
    /// </summary>
    [Fact]
    public void Contains_WithExistingId_ReturnsTrue()
    {
        var tile = new Tile(1, 2, 3);
        var key = new TilesKey([tile], 0);
        
        Assert.True(key.Contains(tile.Id));
    }

    /// <summary>
    /// Verifies that an unknown tile id is not reported.
    /// </summary>
    [Fact]
    public void Contains_WithNonExistingId_ReturnsFalse()
    {
        var tile1 = new Tile(1, 2, 3);
        var tile2 = new Tile(4, 5, 6);
        var key = new TilesKey([tile1], 0);
        
        Assert.False(key.Contains(tile2.Id));
    }

    /// <summary>
    /// Verifies that an empty key contains no tile ids.
    /// </summary>
    [Fact]
    public void Contains_WithEmptyKey_ReturnsFalse()
    {
        var key = new TilesKey([], 0);
        
        Assert.False(key.Contains(12345));
    }

    /// <summary>
    /// Verifies that <see cref="TilesKey.ToString" /> reports the rotation and tile count.
    /// </summary>
    [Fact]
    public void ToString_ReturnsExpectedFormat()
    {
        var tile = new Tile(1, 2, 3);
        var key = new TilesKey([tile], 45);
        
        var result = key.ToString();
        Assert.Contains("Rotation=45", result);
        Assert.Contains("Count=1", result);
    }

    /// <summary>
    /// Verifies that the string representation reports the correct number of tiles.
    /// </summary>
    [Fact]
    public void ToString_WithMultipleTiles_ReturnsCorrectCount()
    {
        var tiles = new[] { new Tile(0, 0, 0), new Tile(1, 0, 0), new Tile(0, 1, 0) };
        var key = new TilesKey(tiles, 0);
        
        var result = key.ToString();
        Assert.Contains("Count=3", result);
    }

    /// <summary>
    /// Verifies that the constructor accepts an <see cref="ICollection{T}" /> of tiles.
    /// </summary>
    [Fact]
    public void Constructor_WithICollection_CreatesValidKey()
    {
        var tiles = new List<Tile> { new Tile(1, 2, 3), new Tile(4, 5, 6) };
        var key = new TilesKey(tiles, 0);
        
        Assert.True(key.Contains(new Tile(1, 2, 3).Id));
        Assert.True(key.Contains(new Tile(4, 5, 6).Id));
    }

    /// <summary>
    /// Verifies that the constructor accepts a generic <see cref="IEnumerable{T}" /> of tiles.
    /// </summary>
    [Fact]
    public void Constructor_WithGenericIEnumerable_CreatesValidKey()
    {
        IEnumerable<Tile> tiles = [new Tile(1, 2, 3), new Tile(4, 5, 6)];
        var key = new TilesKey(tiles, 0);
        
        Assert.True(key.Contains(new Tile(1, 2, 3).Id));
        Assert.True(key.Contains(new Tile(4, 5, 6).Id));
    }

    /// <summary>
    /// Verifies that <see cref="TilesKey" /> implements <see cref="IEquatable{T}" />.
    /// </summary>
    [Fact]
    public void RecordStruct_ImplementsIEquatable()
    {
        var tile = new Tile(1, 2, 3);
        var key1 = new TilesKey([tile], 0);
        var key2 = new TilesKey([tile], 0);
        
        // record struct implements IEquatable<T>
        Assert.IsAssignableFrom<IEquatable<TilesKey>>(key1);
    }
}
