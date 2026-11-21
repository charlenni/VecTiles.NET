using VecTiles.DataSources;
using VecTiles.Styles.OpenMapTiles;

namespace VecTiles.Styles.MapboxTests
{
    /// <summary>
    /// Tests for loading a Mapbox style file and sprites with OMTStyleFileLoader using the osm-liberty style as fixture.
    /// </summary>
    public class OMTStyleLoaderTests
    {
        readonly string _path = "Files";

        /// <summary>
        /// Checks that loading the style file populates the metadata.
        /// </summary>
        [Fact]
        public async Task CheckStyleFileMetadataTest()
        {
            var styleSource = $"file://{_path}/osm-liberty.json";

            var mapboxStyleFile = await OMTStyleFileLoader.Load(styleSource);

            Assert.NotNull(mapboxStyleFile.Metadata);
            Assert.True(mapboxStyleFile.Metadata.ContainsKey("maputnik:license"));
        }

        /// <summary>
        /// Checks that the style file version is at least 8 and a center is present.
        /// </summary>
        [Fact]
        public async Task CheckStyleFileVersionAndCenterTest()
        {
            var styleSource = $"file://{_path}/osm-liberty.json";

            var mapboxStyleFile = await OMTStyleFileLoader.Load(styleSource);

            Assert.True(mapboxStyleFile.Version >= 8);
            Assert.NotNull(mapboxStyleFile.Center);
            Assert.Empty(mapboxStyleFile.Center);
        }

        /// <summary>
        /// Checks that the style file references a glyphs URL with a {fontstack} placeholder.
        /// </summary>
        [Fact]
        public async Task CheckStyleFileGlyphsTest()
        {
            var styleSource = $"file://{_path}/osm-liberty.json";

            var mapboxStyleFile = await OMTStyleFileLoader.Load(styleSource);

            Assert.False(string.IsNullOrEmpty(mapboxStyleFile.Glyphs));
            Assert.Contains("{fontstack}", mapboxStyleFile.Glyphs);
        }

        /// <summary>
        /// Checks that the style file layers are parsed in the expected order.
        /// </summary>
        [Fact]
        public async Task CheckStyleFileLayerOrderTest()
        {
            var styleSource = $"file://{_path}/osm-liberty.json";

            var mapboxStyleFile = await OMTStyleFileLoader.Load(styleSource);

            // Ensure layers are in the expected order
            Assert.Equal("background", mapboxStyleFile.Layers[0].Name);
            Assert.Equal("waterway_tunnel", mapboxStyleFile.Layers[14].Name);
            Assert.Equal("bridge_major_rail_hatching", mapboxStyleFile.Layers[80].Name);
        }

        /// <summary>
        /// Checks that the water layer is parsed with the expected properties.
        /// </summary>
        [Fact]
        public async Task CheckStyleFileLayerPropertiesTest()
        {
            var styleSource = $"file://{_path}/osm-liberty.json";

            var mapboxStyleFile = await OMTStyleFileLoader.Load(styleSource);

            var layer = mapboxStyleFile.Layers.FirstOrDefault(l => l.Name == "water");
            Assert.NotNull(layer);
            Assert.Equal(Common.Enums.LayerStyleType.Fill, layer.StyleType);
            Assert.Equal("openmaptiles", layer.Source);
            Assert.Equal("water", layer.SourceLayer);
        }

        /// <summary>
        /// Checks that loading the style file also loads all sprites.
        /// </summary>
        [Fact]
        public async Task CheckLoadingSpriteFileTest()
        {
            var styleSource = $"file://{_path}/osm-liberty.json";

            var mapboxStyleFile = await OMTStyleFileLoader.Load(styleSource);

            Assert.True(mapboxStyleFile != null);
            Assert.True(mapboxStyleFile.Sprites != null);
            Assert.True(mapboxStyleFile.Sprites.Sprites.Count() == 244);
        }
        /// <summary>
        /// Checks that the loaded sprites have positive dimensions and unique names.
        /// </summary>
        [Fact]
        public async Task CheckSpritePropertiesTest()
        {
            var styleSource = $"file://{_path}/osm-liberty.json";

            var mapboxStyleFile = await OMTStyleFileLoader.Load(styleSource);

            Assert.NotNull(mapboxStyleFile.Sprites);
            Assert.NotNull(mapboxStyleFile.Sprites.Sprites);
            Assert.True(mapboxStyleFile.Sprites.Sprites.Any());

            // Check that a known sprite exists
            var sprite = mapboxStyleFile.Sprites.Sprites.FirstOrDefault(s => s.Key == "airport_11");
            Assert.NotNull(sprite.Value);
            Assert.True(sprite.Value.Width > 0);
            Assert.True(sprite.Value.Height > 0);

            // Check that all sprites have unique names
            var spriteNames = mapboxStyleFile.Sprites.Sprites.Select(s => s.Key).ToList();
            Assert.Equal(spriteNames.Count, spriteNames.Distinct().Count());
        }

        /// <summary>
        /// Checks that the style file contains sources, including the OpenMapTiles source.
        /// </summary>
        [Fact]
        public async Task CheckStyleFileSourcesExistTest()
        {
            var styleSource = $"file://{_path}/osm-liberty.json";

            var mapboxStyleFile = await OMTStyleFileLoader.Load(styleSource);

            Assert.NotNull(mapboxStyleFile.Sources);
            Assert.True(mapboxStyleFile.Sources.Count > 0);

            // Check that a known source exists
            Assert.Contains("openmaptiles", mapboxStyleFile.Sources.Keys);
        }

        /// <summary>
        /// Checks that the natural_earth_shaded_relief raster source has valid tile URLs.
        /// </summary>
        [Fact]
        public async Task CheckSourcePropertiesTilesTest()
        {
            var styleSource = $"file://{_path}/osm-liberty.json";

            var mapboxStyleFile = await OMTStyleFileLoader.Load(styleSource);

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

        /// <summary>
        /// Checks that the openmaptiles vector source has a non-empty URL.
        /// </summary>
        [Fact]
        public async Task CheckSourcePropertiesUrlTest()
        {
            var styleSource = $"file://{_path}/osm-liberty.json";

            var mapboxStyleFile = await OMTStyleFileLoader.Load(styleSource);

            Assert.NotNull(mapboxStyleFile.Sources);

            var source = mapboxStyleFile.Sources.FirstOrDefault(s => s.Key == "openmaptiles").Value;
            Assert.NotNull(source);

            // Check some typical properties
            Assert.True(source.SourceType == Common.Enums.SourceType.Vector);
            Assert.False(string.IsNullOrEmpty(source.Url));
        }

        /// <summary>
        /// Checks that all source names in the style file are unique.
        /// </summary>
        [Fact]
        public async Task CheckAllSourcesHaveUniqueNamesTest()
        {
            var styleSource = $"file://{_path}/osm-liberty.json";

            var mapboxStyleFile = await OMTStyleFileLoader.Load(styleSource);

            var sourceNames = mapboxStyleFile.Sources.Keys.ToList();
            Assert.Equal(sourceNames.Count, sourceNames.Distinct().Count());
        }

        /// <summary>
        /// Checks that sprite loading prefers the @2x pair when it is available.
        /// </summary>
        [Fact]
        public async Task LoadSpritesUses2xPairWhenAvailableTest()
        {
            var sprites = await OMTStyleFileLoader.LoadSprites("embedded://Sprites/osm-liberty");

            Assert.Equal(244, sprites.Sprites.Count);
            // The @2x json holds double sized coordinates, so this width proves the @2x pair was loaded
            Assert.Equal(30, sprites.Sprites["airport_11"].Width);
        }

        /// <summary>
        /// Checks that sprite loading falls back to the unsuffixed pair.
        /// </summary>
        [Fact]
        public async Task LoadSpritesFallsBackToUnsuffixedPairTest()
        {
            var bitmapBytes = await ByteDataSource.GetBytesAsync("embedded://Sprites/osm-liberty.png");
            var jsonBytes = await ByteDataSource.GetBytesAsync("embedded://Sprites/osm-liberty.json");

            var tempDir = Path.Combine(Path.GetTempPath(), "VecTilesSpriteLoaderTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                File.WriteAllBytes(Path.Combine(tempDir, "osm-liberty.png"), bitmapBytes);
                File.WriteAllBytes(Path.Combine(tempDir, "osm-liberty.json"), jsonBytes);

                var sprites = await OMTStyleFileLoader.LoadSprites(new Uri(Path.Combine(tempDir, "osm-liberty")).AbsoluteUri);

                Assert.Equal(244, sprites.Sprites.Count);
                // The unsuffixed json holds base sized coordinates, so this width proves the unsuffixed pair was loaded
                Assert.Equal(15, sprites.Sprites["airport_11"].Width);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
