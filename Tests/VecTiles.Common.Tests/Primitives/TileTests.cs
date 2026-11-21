using VecTiles.Common.Primitives;

namespace VecTiles.Common.Tests.Primitives
{
    /// <summary>
    /// Unit tests for the <see cref="Tile" /> primitive.
    /// </summary>
    public class TileTests
    {
        /// <summary>
        /// Verifies that <see cref="Tile.CalculateTileId(int, int, int)" /> and <see cref="Tile.CalculateTile" />
        /// round-trip between tile coordinates and tile id.
        /// </summary>
        /// <param name="x">Tile x coordinate.</param>
        /// <param name="y">Tile y coordinate.</param>
        /// <param name="zoom">Zoom level of the tile.</param>
        /// <param name="expectedId">Expected tile id for the given coordinates.</param>
        [Theory]
        [InlineData(0, 0, 0, 0UL)]
        [InlineData(1, 0, 1, 2UL)]
        [InlineData(1, 1, 1, 4UL)]
        [InlineData(2, 2, 2, 15UL)]
        public void CalculateTileId_And_CalculateTile_AreInverses(int x, int y, int zoom, ulong expectedId)
        {
            var id = Tile.CalculateTileId(zoom, x, y);
            Assert.Equal(expectedId, id);

            var (rx, ry, rzoom) = Tile.CalculateTile(id);
            Assert.Equal(x, rx);
            Assert.Equal(y, ry);
            Assert.Equal(zoom, rzoom);
        }

        /// <summary>
        /// Verifies that a tile constructed from its id exposes the correct x/y/zoom properties.
        /// </summary>
        [Fact]
        public void Tile_ConstructedWithId_HasCorrectProperties()
        {
            var tile = new Tile(0UL);
            Assert.Equal(0, tile.X);
            Assert.Equal(0, tile.Y);
            Assert.Equal(0, tile.Zoom);
            Assert.Equal(0UL, tile.Id);
            Assert.True(tile.IsValid);
        }

        /// <summary>
        /// Verifies that a tile constructed from x/y/zoom computes the matching tile id.
        /// </summary>
        [Fact]
        public void Tile_ConstructedWithXYZoom_HasCorrectId()
        {
            var tile = new Tile(1, 2, 3);
            var expectedId = Tile.CalculateTileId(3, 1, 2);
            Assert.Equal(expectedId, tile.Id);
            Assert.Equal(1, tile.X);
            Assert.Equal(2, tile.Y);
            Assert.Equal(3, tile.Zoom);
        }

        /// <summary>
        /// Verifies equality semantics and hash codes of tiles with identical or different coordinates.
        /// </summary>
        [Fact]
        public void Tile_Equals_And_GetHashCode_Work()
        {
            var t1 = new Tile(1, 2, 3);
            var t2 = new Tile(1, 2, 3);
            var t3 = new Tile(2, 2, 3);
            Assert.True(t1.Equals(t2));
            Assert.False(t1.Equals(t3));
            Assert.Equal(t1.GetHashCode(), t2.GetHashCode());
        }

        /// <summary>
        /// Verifies the "x-y@z" string format of <see cref="Tile.ToString" />.
        /// </summary>
        [Fact]
        public void Tile_ToString_ReturnsExpectedFormat()
        {
            var tile = new Tile(1, 2, 3);
            Assert.Equal("1x-2y@3z", tile.ToString());
        }

        /// <summary>
        /// Verifies that <see cref="Tile.Parent" /> returns the enclosing tile at one zoom level up.
        /// </summary>
        [Fact]
        public void Tile_Parent_ReturnsCorrectTile()
        {
            var tile = new Tile(2, 2, 3);
            var parent = tile.Parent;
            Assert.Equal(1, parent.X);
            Assert.Equal(1, parent.Y);
            Assert.Equal(2, parent.Zoom);
        }

        /// <summary>
        /// Verifies that <see cref="Tile.IsDirectNeighbour" /> recognizes horizontal and vertical
        /// neighbours but not the tile itself.
        /// </summary>
        [Fact]
        public void Tile_IsDirectNeighbour_Works()
        {
            var t1 = new Tile(1, 1, 2);
            var t2 = new Tile(1, 2, 2);
            var t3 = new Tile(2, 1, 2);
            var t4 = new Tile(1, 1, 2);
            Assert.True(Tile.IsDirectNeighbour(t1.Id, t2.Id));
            Assert.True(Tile.IsDirectNeighbour(t1.Id, t3.Id));
            Assert.False(Tile.IsDirectNeighbour(t1.Id, t4.Id));
        }

        /// <summary>
        /// Verifies that <see cref="Tile.GetSubTiles" /> returns the 2x2 sub range at the deeper zoom.
        /// </summary>
        [Fact]
        public void Tile_GetSubTiles_ReturnsCorrectRange()
        {
            var tile = new Tile(1, 1, 2);
            var range = tile.GetSubTiles(3);
            Assert.Equal(2, range.XMin);
            Assert.Equal(2, range.YMin);
            Assert.Equal(3, range.XMax);
            Assert.Equal(3, range.YMax);
            Assert.Equal(3, range.Zoom);
        }

        /// <summary>
        /// Verifies that <see cref="Tile.InvertX" /> and <see cref="Tile.InvertY" />
        /// mirror the respective coordinate at the zoom level.
        /// </summary>
        [Fact]
        public void Tile_InvertX_And_InvertY_Work()
        {
            var tile = new Tile(1, 2, 3);
            var invX = tile.InvertX();
            var invY = tile.InvertY();
            Assert.Equal(6, invX.X);
            Assert.Equal(2, invX.Y);
            Assert.Equal(1, invY.X);
            Assert.Equal(5, invY.Y);
        }
    }
}
