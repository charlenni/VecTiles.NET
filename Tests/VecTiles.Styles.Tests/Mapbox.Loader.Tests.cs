using VecTiles.Styles.Mapbox;

namespace VecTiles.Styles.MapboxTests
{
    public class MapboxLoaderTests
    {
        readonly string _path = "files";

        [Fact]
        public async Task CheckStyleFileMetadataTest()
        {
            var stream = File.Open(Path.Combine(_path, "osm-liberty.json"), FileMode.Open, FileAccess.Read);

            var mapboxStyleFile = await MapboxStyleFileLoader.Load(stream);

            Assert.NotNull(mapboxStyleFile.Metadata);
            Assert.True(mapboxStyleFile.Metadata.ContainsKey("maputnik:license"));
        }

        [Fact]
        public async Task CheckStyleFileVersionAndCenterTest()
        {
            var stream = File.Open(Path.Combine(_path, "osm-liberty.json"), FileMode.Open, FileAccess.Read);

            var mapboxStyleFile = await MapboxStyleFileLoader.Load(stream);

            Assert.True(mapboxStyleFile.Version >= 8);
            Assert.NotNull(mapboxStyleFile.Center);
            Assert.Equal(0, mapboxStyleFile.Center.Length);
        }

        [Fact]
        public async Task CheckStyleFileGlyphsTest()
        {
            var stream = File.Open(Path.Combine(_path, "osm-liberty.json"), FileMode.Open, FileAccess.Read);

            var mapboxStyleFile = await MapboxStyleFileLoader.Load(stream);

            Assert.False(string.IsNullOrEmpty(mapboxStyleFile.Glyphs));
            Assert.Contains("{fontstack}", mapboxStyleFile.Glyphs);
        }

        [Fact]
        public async Task CheckStyleFileLayerOrderTest()
        {
            var stream = File.Open(Path.Combine(_path, "osm-liberty.json"), FileMode.Open, FileAccess.Read);

            var mapboxStyleFile = await MapboxStyleFileLoader.Load(stream);

            // Ensure layers are in the expected order
            Assert.Equal("background", mapboxStyleFile.Layers[0].Name);
            Assert.Equal("water", mapboxStyleFile.Layers[14].Name);
            Assert.Equal("building", mapboxStyleFile.Layers[80].Name);
        }

        [Fact]
        public async Task CheckStyleFileLayerPropertiesTest()
        {
            var stream = File.Open(Path.Combine(_path, "osm-liberty.json"), FileMode.Open, FileAccess.Read);

            var mapboxStyleFile = await MapboxStyleFileLoader.Load(stream);

            var layer = mapboxStyleFile.Layers.FirstOrDefault(l => l.Name == "water");
            Assert.NotNull(layer);
            Assert.Equal(Common.Enums.LayerStyleType.Fill, layer.StyleType);
            Assert.Equal("openmaptiles", layer.Source);
            Assert.Equal("water", layer.SourceLayer);
        }

        [Fact]
        public async Task CheckLoadingSpriteFileTest()
        {
            var stream = File.Open(Path.Combine(_path, "osm-liberty.json"), FileMode.Open, FileAccess.Read);

            var mapboxStyleFile = await MapboxStyleFileLoader.Load(stream);

            Assert.True(mapboxStyleFile != null);
            Assert.True(mapboxStyleFile.Sprites != null);
            Assert.True(mapboxStyleFile.Sprites.Sprites.Count() == 241);
        }
    }
}
