using VecTiles.Common.Primitives;

namespace VecTiles.Common.Tests.Primitives
{
    public class TileRangeTests
    {
        [Fact]
        public void Constructor_SetsPropertiesCorrectly()
        {
            var range = new TileRange(1, 2, 3, 4, 5);
            Assert.Equal(1, range.XMin);
            Assert.Equal(2, range.YMin);
            Assert.Equal(3, range.XMax);
            Assert.Equal(4, range.YMax);
            Assert.Equal(5, range.Zoom);
        }

        [Fact]
        public void Count_ReturnsCorrectNumberOfTiles()
        {
            var range = new TileRange(0, 0, 2, 2, 1);
            Assert.Equal(9, range.Count);
        }

        [Fact]
        public void Contains_ReturnsTrueForTileInRange()
        {
            var range = new TileRange(0, 0, 2, 2, 1);
            var tile = new Tile(1, 1, 1);
            Assert.True(range.Contains(tile));
        }

        [Fact]
        public void Contains_ReturnsFalseForTileOutOfRange()
        {
            var range = new TileRange(0, 0, 2, 2, 1);
            var tile = new Tile(3, 3, 1);
            Assert.False(range.Contains(tile));
        }

        [Fact]
        public void IsBorderAt_ReturnsTrueForBorderTile()
        {
            var range = new TileRange(0, 0, 2, 2, 1);
            Assert.True(range.IsBorderAt(0, 1, 1));
            Assert.True(range.IsBorderAt(2, 1, 1));
            Assert.True(range.IsBorderAt(1, 0, 1));
            Assert.True(range.IsBorderAt(1, 2, 1));
        }

        [Fact]
        public void IsBorderAt_ReturnsFalseForNonBorderTile()
        {
            var range = new TileRange(0, 0, 2, 2, 1);
            Assert.False(range.IsBorderAt(1, 1, 1));
        }

        [Fact]
        public void Enumerator_EnumeratesAllTiles()
        {
            var range = new TileRange(0, 0, 1, 1, 1);
            var tiles = range.ToList();
            Assert.Equal(4, tiles.Count);
            Assert.Contains(tiles, t => t!.X == 0 && t.Y == 0 && t.Zoom == 1);
            Assert.Contains(tiles, t => t!.X == 1 && t.Y == 0 && t.Zoom == 1);
            Assert.Contains(tiles, t => t!.X == 0 && t.Y == 1 && t.Zoom == 1);
            Assert.Contains(tiles, t => t!.X == 1 && t.Y == 1 && t.Zoom == 1);
        }

        [Fact]
        public void EnumerateInCenterFirst_EnumeratesAllTiles()
        {
            var range = new TileRange(0, 0, 2, 2, 1);
            var tiles = range.EnumerateInCenterFirst().ToList();
            Assert.Equal(9, tiles.Count);
            var allTiles = new HashSet<(int, int)>(tiles.Select(t => (t!.X, t.Y)));
            for (int x = 0; x <= 2; x++)
                for (int y = 0; y <= 2; y++)
                    Assert.Contains((x, y), allTiles);
        }

        [Fact]
        public void TileRangeCenteredEnumerator_EnumeratesAllTiles()
        {
            var range = new TileRange(0, 0, 2, 2, 1);
            var enumerator = new TileRange.TileRangeCenteredEnumerator(range);
            var tiles = new List<Tile?>();
            while (enumerator.MoveNext())
            {
                tiles.Add(enumerator.Current);
            }
            Assert.Equal(9, tiles.Count);
            var allTiles = new HashSet<(int, int)>(tiles.Select(t => (t!.X, t.Y)));
            for (int x = 0; x <= 2; x++)
                for (int y = 0; y <= 2; y++)
                    Assert.Contains((x, y), allTiles);
        }
    }
}
