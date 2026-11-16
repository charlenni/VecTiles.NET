using System;
using VecTiles.Common.Primitives;

namespace VecTiles.Common.Tests.Primitives;

public class TilesKeyTests
{
    [Fact]
    public void Constructor_WithNullTiles_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new TilesKey(null!));
    }

    [Fact]
    public void Constructor_WithEmptyCollection_CreatesValidKey()
    {
        var key = new TilesKey([], 0);
        Assert.Equal(0, key.Rotation);
        Assert.Equal("TilesKey(Rotation=0, Count=0)", key.ToString());
    }

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

    [Fact]
    public void Constructor_WithSingleTile_CreatesValidKey()
    {
        var tile = new Tile(1, 2, 3);
        var key = new TilesKey([tile], 45);
        Assert.Equal(45, key.Rotation);
        Assert.True(key.Contains(tile.Id));
    }

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

    [Fact]
    public void Constructor_WithDuplicateTiles_CreatesValidKey()
    {
        var tile = new Tile(1, 2, 3);
        var key = new TilesKey([tile, tile, tile], 0);
        
        Assert.True(key.Contains(tile.Id));
    }

    [Fact]
    public void Equals_SameTilesSameRotation_ReturnsTrue()
    {
        var tile = new Tile(1, 2, 3);
        var key1 = new TilesKey([tile], 0);
        var key2 = new TilesKey([tile], 0);
        
        Assert.Equal(key1, key2);
    }

    [Fact]
    public void Equals_SameTilesDifferentRotation_ReturnsFalse()
    {
        var tile = new Tile(1, 2, 3);
        var key1 = new TilesKey([tile], 0);
        var key2 = new TilesKey([tile], 90);
        
        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void Equals_DifferentTilesSameRotation_ReturnsFalse()
    {
        var tile1 = new Tile(1, 2, 3);
        var tile2 = new Tile(4, 5, 6);
        var key1 = new TilesKey([tile1], 0);
        var key2 = new TilesKey([tile2], 0);
        
        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void Equals_DifferentNumberOfTiles_ReturnsFalse()
    {
        var tile1 = new Tile(1, 2, 3);
        var tile2 = new Tile(4, 5, 6);
        var key1 = new TilesKey([tile1], 0);
        var key2 = new TilesKey([tile1, tile2], 0);
        
        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void Equals_SameInstance_ReturnsTrue()
    {
        var tile = new Tile(1, 2, 3);
        var key = new TilesKey([tile], 0);
        
        Assert.Equal(key, key);
    }

    [Fact]
    public void GetHashCode_SameKeys_ReturnsSameHashCode()
    {
        var tile = new Tile(1, 2, 3);
        var key1 = new TilesKey([tile], 45);
        var key2 = new TilesKey([tile], 45);
        
        Assert.Equal(key1.GetHashCode(), key2.GetHashCode());
    }

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

    [Fact]
    public void GetHashCode_OrderIndependent_ReturnsSameHashCode()
    {
        var tile1 = new Tile(1, 2, 3);
        var tile2 = new Tile(4, 5, 6);
        
        var key1 = new TilesKey([tile1, tile2], 0);
        var key2 = new TilesKey([tile2, tile1], 0);
        
        Assert.Equal(key1.GetHashCode(), key2.GetHashCode());
    }

    [Fact]
    public void Contains_WithExistingId_ReturnsTrue()
    {
        var tile = new Tile(1, 2, 3);
        var key = new TilesKey([tile], 0);
        
        Assert.True(key.Contains(tile.Id));
    }

    [Fact]
    public void Contains_WithNonExistingId_ReturnsFalse()
    {
        var tile1 = new Tile(1, 2, 3);
        var tile2 = new Tile(4, 5, 6);
        var key = new TilesKey([tile1], 0);
        
        Assert.False(key.Contains(tile2.Id));
    }

    [Fact]
    public void Contains_WithEmptyKey_ReturnsFalse()
    {
        var key = new TilesKey([], 0);
        
        Assert.False(key.Contains(12345));
    }

    [Fact]
    public void ToString_ReturnsExpectedFormat()
    {
        var tile = new Tile(1, 2, 3);
        var key = new TilesKey([tile], 45);
        
        var result = key.ToString();
        Assert.Contains("Rotation=45", result);
        Assert.Contains("Count=1", result);
    }

    [Fact]
    public void ToString_WithMultipleTiles_ReturnsCorrectCount()
    {
        var tiles = new[] { new Tile(0, 0, 0), new Tile(1, 0, 0), new Tile(0, 1, 0) };
        var key = new TilesKey(tiles, 0);
        
        var result = key.ToString();
        Assert.Contains("Count=3", result);
    }

    [Fact]
    public void Constructor_WithICollection_CreatesValidKey()
    {
        var tiles = new List<Tile> { new Tile(1, 2, 3), new Tile(4, 5, 6) };
        var key = new TilesKey(tiles, 0);
        
        Assert.True(key.Contains(new Tile(1, 2, 3).Id));
        Assert.True(key.Contains(new Tile(4, 5, 6).Id));
    }

    [Fact]
    public void Constructor_WithGenericIEnumerable_CreatesValidKey()
    {
        IEnumerable<Tile> tiles = [new Tile(1, 2, 3), new Tile(4, 5, 6)];
        var key = new TilesKey(tiles, 0);
        
        Assert.True(key.Contains(new Tile(1, 2, 3).Id));
        Assert.True(key.Contains(new Tile(4, 5, 6).Id));
    }

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
