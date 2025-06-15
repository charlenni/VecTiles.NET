using VecTiles.Styles.Mapbox;

namespace VecTiles.Styles.MapboxTests
{
    public class MapboxStyleLoaderTests
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
            Assert.Empty(mapboxStyleFile.Center);
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
        [Fact]
        public async Task CheckSpritePropertiesTest()
        {
            var stream = File.Open(Path.Combine(_path, "osm-liberty.json"), FileMode.Open, FileAccess.Read);
            var mapboxStyleFile = await MapboxStyleFileLoader.Load(stream);

            Assert.NotNull(mapboxStyleFile.Sprites);
            Assert.NotNull(mapboxStyleFile.Sprites.Sprites);
            Assert.True(mapboxStyleFile.Sprites.Sprites.Any());

            // Check that a known sprite exists
            var sprite = mapboxStyleFile.Sprites.Sprites.FirstOrDefault(s => s.Key == "airport_15");
            Assert.NotNull(sprite.Value);
            Assert.True(sprite.Value.Width > 0);
            Assert.True(sprite.Value.Height > 0);

            // Check that all sprites have unique names
            var spriteNames = mapboxStyleFile.Sprites.Sprites.Select(s => s.Key).ToList();
            Assert.Equal(spriteNames.Count, spriteNames.Distinct().Count());
        }

        [Fact]
        public async Task CheckStyleFileSourcesExistTest()
        {
            var stream = File.Open(Path.Combine(_path, "osm-liberty.json"), FileMode.Open, FileAccess.Read);
            var mapboxStyleFile = await MapboxStyleFileLoader.Load(stream);

            Assert.NotNull(mapboxStyleFile.Sources);
            Assert.True(mapboxStyleFile.Sources.Count > 0);

            // Check that a known source exists
            Assert.Contains("openmaptiles", mapboxStyleFile.Sources.Keys);
        }

        [Fact]
        public async Task CheckSourcePropertiesTilesTest()
        {
            var stream = File.Open(Path.Combine(_path, "osm-liberty.json"), FileMode.Open, FileAccess.Read);
            var mapboxStyleFile = await MapboxStyleFileLoader.Load(stream);

            Assert.NotNull(mapboxStyleFile.Sources);

            var source = mapboxStyleFile.Sources.FirstOrDefault(s => s.Key == "natural_earth_shaded_relief").Value;
            Assert.NotNull(source);

            // Check some typical properties
            Assert.True(source.SourceType == Common.Enums.SourceType.Raster);

            // If tiles property exists, it should be a non-empty list
            if (source.Tiles != null)
            {
                Assert.NotEmpty(source.Tiles);
                foreach (var tileUrl in source.Tiles)
                {
                    Assert.False(string.IsNullOrEmpty(tileUrl));
                }
            }
        }

        [Fact]
        public async Task CheckSourcePropertiesUrlTest()
        {
            var stream = File.Open(Path.Combine(_path, "osm-liberty.json"), FileMode.Open, FileAccess.Read);
            var mapboxStyleFile = await MapboxStyleFileLoader.Load(stream);

            Assert.NotNull(mapboxStyleFile.Sources);

            var source = mapboxStyleFile.Sources.FirstOrDefault(s => s.Key == "openmaptiles").Value;
            Assert.NotNull(source);

            // Check some typical properties
            Assert.True(source.SourceType == Common.Enums.SourceType.Vector);
            Assert.False(string.IsNullOrEmpty(source.Url));
        }

        [Fact]
        public async Task CheckAllSourcesHaveUniqueNamesTest()
        {
            var stream = File.Open(Path.Combine(_path, "osm-liberty.json"), FileMode.Open, FileAccess.Read);
            var mapboxStyleFile = await MapboxStyleFileLoader.Load(stream);

            var sourceNames = mapboxStyleFile.Sources.Keys.ToList();
            Assert.Equal(sourceNames.Count, sourceNames.Distinct().Count());
        }
    }
}
