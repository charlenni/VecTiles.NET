using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using VecTiles.Common.Enums;
using VecTiles.Common.Primitives;
using VecTiles.Styles.Mapsforge;
using VecTiles.Styles.Mapsforge.Enums;

using VecTiles.Tests;

namespace VecTiles.Styles.Mapsforge.Tests
{
    /// <summary>
    /// Tests for loading a Mapsforge render theme with the MFRenderThemeLoader
    /// using the default.xml theme of the repository as fixture, plus synthetic
    /// themes for rule attribute handling.
    /// </summary>
    public class MFRenderThemeLoaderTests
    {
        private readonly string _themes = RepoData.MapsforgeFiles;

        private static Task<MFRenderTheme> LoadTheme(string path, string? sourceName = null)
        {
            return MFRenderThemeLoader.Load(path, sourceName);
        }

        /// <summary>
        /// Writes a synthetic render theme into a temporary folder and returns
        /// its path (the folder is removed by the returned action).
        /// </summary>
        private static (string Path, Action Cleanup) WriteTheme(string themeXml)
        {
            var directory = Path.Combine(Path.GetTempPath(), "VecTilesRenderThemeTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);

            var path = Path.Combine(directory, "test.xml");
            File.WriteAllText(path, themeXml);

            return (path, () => Directory.Delete(directory, true));
        }

        // --- default.xml ---

        /// <summary>
        /// Checks that the global attributes of default.xml are read.
        /// </summary>
        [Fact]
        public async Task LoadDefaultThemeGlobalsTest()
        {
            var theme = await LoadTheme(Path.Combine(_themes, "mapsforge", "default.xml"));

            Assert.Equal("mapsforge", theme.Name);
            Assert.Equal("mapsforge", theme.Source);
            Assert.Equal(5, theme.Version);
            Assert.Equal(new Color(0xF8, 0xF8, 0xF8, 255), theme.MapBackground);
            Assert.Equal(new Color(0xDD, 0xDD, 0xDD, 255), theme.MapBackgroundOutside);
            Assert.Equal(1f, theme.BaseStrokeWidth);
            Assert.Equal(1f, theme.BaseTextSize);
            Assert.Null(theme.StyleMenu);
        }

        /// <summary>
        /// Checks that loading default.xml produces a large, ordered list of
        /// layers with unique names, all rendering from the mapsforge source.
        /// </summary>
        [Fact]
        public async Task LoadDefaultThemeLayersTest()
        {
            var theme = await LoadTheme(Path.Combine(_themes, "mapsforge", "default.xml"));

            Assert.True(theme.Layers.Count > 100);

            foreach (var layer in theme.Layers)
            {
                Assert.Equal("mapsforge", layer.Source);
                Assert.Equal("mapsforge", layer.SourceLayer);
            }

            var names = theme.Layers.Select(l => l.Name).ToList();
            Assert.Equal(names.Count, names.Distinct().Count());
        }

        /// <summary>
        /// Checks that the first rule of default.xml (sea areas) becomes the
        /// first fill layer with the combined tag and closed filter.
        /// </summary>
        [Fact]
        public async Task LoadDefaultThemeFirstLayerIsSeaAreaTest()
        {
            var theme = await LoadTheme(Path.Combine(_themes, "mapsforge", "default.xml"));

            var layer = Assert.IsType<MFFillLayerStyle>(theme.Layers[0]);

            Assert.Equal("area1", layer.Name);
            Assert.Equal(LayerStyleType.Fill, layer.StyleType);
            Assert.Equal(new Color(0xB3, 0xDD, 0xFF, 255), layer.Fill);

            var polygon = new Polygon(new LinearRing([new Coordinate(0, 0), new Coordinate(1, 0),
                new Coordinate(1, 1), new Coordinate(0, 0)]));
            var line = new LineString([new Coordinate(0, 0), new Coordinate(1, 1)]);

            // Areas require closed ways with one of the tags natural=issea|sea
            var context = new EvaluationContext(5, feature: new Feature(polygon, new NetTopologySuite.Features.AttributesTable { { "natural", "sea" } }));
            Assert.Equal(true, layer.Filter.Evaluate(context));

            context = new EvaluationContext(5, feature: new Feature(polygon, new NetTopologySuite.Features.AttributesTable { { "natural", "forest" } }));
            Assert.Equal(false, layer.Filter.Evaluate(context));

            context = new EvaluationContext(5, feature: new Feature(line, new NetTopologySuite.Features.AttributesTable { { "natural", "sea" } }));
            Assert.Equal(false, layer.Filter.Evaluate(context));
        }

        /// <summary>
        /// Regression test of the place-of-worship icons ("Grosse Kirche Fluntern",
        /// 15/17162/11474): the church polygon carries amenity=place_of_worship,
        /// building=church and religion=christian, so the christian symbol layers must match
        /// and the unknown religion fallback (rule religion=~|*) must not.
        /// </summary>
        [Fact]
        public async Task LoadDefaultThemeChurchSymbolFiltersTest()
        {
            var theme = await LoadTheme(Path.Combine(_themes, "mapsforge", "default.xml"));

            var church = new Feature(
                new Polygon(new LinearRing([new Coordinate(0, 0), new Coordinate(10, 0),
                    new Coordinate(10, 10), new Coordinate(0, 0)])),
                new AttributesTable
                {
                    { "amenity", "place_of_worship" },
                    { "building", "church" },
                    { "religion", "christian" },
                    { "name", "Grosse Kirche Fluntern" },
                });
            var context = new EvaluationContext(15, feature: church);

            var christian = theme.Layers
                .OfType<MFSymbolLayerStyle>()
                .Where(l => l.Src.EndsWith("place_of_worship/christian.svg", StringComparison.Ordinal))
                .ToList();
            Assert.NotEmpty(christian);
            Assert.All(christian, l => Assert.Equal(true, l.Filter.Evaluate(context)));

            var unknown = theme.Layers
                .OfType<MFSymbolLayerStyle>()
                .Where(l => l.Src.EndsWith("place_of_worship/unknown.svg", StringComparison.Ordinal))
                .ToList();
            Assert.NotEmpty(unknown);
            Assert.All(unknown, l => Assert.Equal(false, l.Filter.Evaluate(context)));

            // Without the religion tag the unknown fallback matches
            var withoutReligion = new Feature(church.Geometry, new AttributesTable
            {
                { "amenity", "place_of_worship" },
            });
            var withoutReligionContext = new EvaluationContext(15, feature: withoutReligion);
            Assert.All(christian, l => Assert.Equal(false, l.Filter.Evaluate(withoutReligionContext)));
            Assert.All(unknown, l => Assert.Equal(true, l.Filter.Evaluate(withoutReligionContext)));
        }

        /// <summary>
        /// Regression test of the drawing order ("Texte unter Straßen"): except for labels and
        /// symbols, all rendering instructions are drawn in the order of their definition
        /// (Rendertheme.md); captions, icons, lineSymbols and pathTexts are labels, the
        /// reference renderer draws them after all fills, lines and circles. The loader must
        /// reorder the flattened layers accordingly, otherwise a caption of an early rule is
        /// overdrawn by the lines of later rules.
        /// </summary>
        [Fact]
        public async Task LoadDefaultThemeSymbolLayersAfterGeometryLayersTest()
        {
            var theme = await LoadTheme(Path.Combine(_themes, "mapsforge", "default.xml"));

            Assert.True(theme.Layers.Count > 100);

            var lastGeometryIndex = -1;
            var firstSymbolIndex = theme.Layers.Count;
            for (var i = 0; i < theme.Layers.Count; i++)
            {
                if (theme.Layers[i].StyleType == LayerStyleType.Symbol)
                {
                    firstSymbolIndex = Math.Min(firstSymbolIndex, i);
                }
                else
                {
                    lastGeometryIndex = i;
                }
            }

            Assert.True(firstSymbolIndex > lastGeometryIndex,
                $"Symbol layer at index {firstSymbolIndex} before geometry layer at index {lastGeometryIndex}");

            // The sea area rule is the first rule of the theme and stays the first layer
            Assert.IsType<MFFillLayerStyle>(theme.Layers[0]);
        }

        /// <summary>
        /// Checks that symbol layers, captions, line symbols and path texts of
        /// default.xml are read with their attributes.
        /// </summary>
        [Fact]
        public async Task LoadDefaultThemeSymbolAttributesTest()
        {
            var theme = await LoadTheme(Path.Combine(_themes, "mapsforge", "default.xml"));

            var lineSymbol = theme.Layers.OfType<MFLineSymbolLayerStyle>()
                .First(l => l.Src == "jar:symbols/oneway.svg");

            Assert.True(lineSymbol.Repeat);
            Assert.Equal(200f, lineSymbol.RepeatGap); // default repeat-gap
            Assert.Equal(MapsforgeDisplay.Order, lineSymbol.Display);
            Assert.Equal(-50, lineSymbol.Priority);

            var pathText = theme.Layers.OfType<MFPathTextLayerStyle>()
                .First(l => l.Display == MapsforgeDisplay.Order && l.Priority == -100);

            Assert.Equal("name", pathText.Key);
            Assert.Equal(MapsforgeFontStyle.Bold, pathText.FontStyle);
            Assert.Equal(14f, pathText.FontSize);

            var captionRef = theme.Layers.OfType<MFCaptionLayerStyle>()
                .First(l => l.SymbolId == "helipad");

            Assert.Equal(MapsforgePosition.Above, captionRef.Position);
            Assert.Equal(-20, captionRef.Priority);
            Assert.NotNull(captionRef.PositionSymbol);
            Assert.Equal("jar:symbols/transport/helicopter.svg", captionRef.PositionSymbolSrc);

            var symbol = theme.Layers.OfType<MFSymbolLayerStyle>()
                .First(l => l.Id == "helipad");

            Assert.Equal("jar:symbols/transport/helicopter.svg", symbol.Src);
        }

        /// <summary>
        /// Checks that captions without a symbol reference of their rule chain
        /// (their icon is drawn by an unrelated rule) have no position symbol.
        /// </summary>
        [Fact]
        public async Task LoadDefaultThemeCaptionWithoutSymbolIdTest()
        {
            var theme = await LoadTheme(Path.Combine(_themes, "mapsforge", "default.xml"));

            var captionRef = theme.Layers.OfType<MFCaptionLayerStyle>()
                .First(l => l.SymbolId == null && l.Position == MapsforgePosition.Above);

            // The icon of the feature is drawn by another rule, so the caption
            // falls back to being centered on the point
            Assert.Null(captionRef.PositionSymbol);
        }

        /// <summary>
        /// Checks that loading a theme through an explicit file:// URL works.
        /// </summary>
        [Fact]
        public async Task LoadThemeWithFileSourceTest()
        {
            var source = $"file://{Path.Combine(_themes, "mapsforge", "default.xml")}";

            var theme = await LoadTheme(source);

            Assert.True(theme.Layers.Count > 0);
            Assert.Equal("mapsforge", theme.Name);
        }

        /// <summary>
        /// Checks that loading a named theme uses the given name and source.
        /// </summary>
        [Fact]
        public async Task LoadThemeWithSourceNameTest()
        {
            var theme = await LoadTheme(Path.Combine(_themes, "mapsforge", "default.xml"), "osm");

            Assert.Equal("osm", theme.Name);
            Assert.Equal("osm", theme.Source);
        }

        /// <summary>
        /// Checks that data of other file types is rejected.
        /// </summary>
        [Fact]
        public async Task LoadInvalidThemeThrowsTest()
        {
            var (path, cleanup) = WriteTheme("<?xml version=\"1.0\"?><randomReport/>");

            try
            {
                await Assert.ThrowsAsync<ArgumentException>(async () => await LoadTheme(path));
            }
            finally
            {
                cleanup();
            }
        }

        /// <summary>
        /// Checks that loading a missing file throws.
        /// </summary>
        [Fact]
        public async Task LoadMissingThemeThrowsTest()
        {
            await Assert.ThrowsAnyAsync<Exception>(async () =>
                await LoadTheme(Path.Combine(_themes, "mapsforge", "does-not-exist.xml")));
        }

        // --- stylemenu ---

        /// <summary>
        /// Checks that the stylemenu of motorider.xml is parsed.
        /// </summary>
        [Fact]
        public async Task LoadStyleMenuTest()
        {
            var theme = await LoadTheme(Path.Combine(_themes, "mapsforge", "motorider.xml"));

            Assert.NotNull(theme.StyleMenu);
            Assert.Equal("menu", theme.StyleMenu.Id);
            Assert.Equal("normal", theme.StyleMenu.DefaultValue);
            Assert.Equal("en", theme.StyleMenu.DefaultLanguage);
            Assert.Contains(theme.StyleMenu.Layers, l => l.Id == "accommodation");

            var layer = theme.StyleMenu.Layers.First(l => l.Id == "accommodation");

            Assert.Equal("Accommodation", layer.Names["en"]);
            Assert.Contains("accommodation", layer.Categories);
        }

        // --- synthetic themes ---

        /// <summary>
        /// Checks the inheritance of zoom and category attributes through the
        /// rule chain and the exclusive upper zoom bound of the layers.
        /// </summary>
        [Fact]
        public async Task LoadRuleZoomAndCatInheritanceTest()
        {
            var (path, cleanup) = WriteTheme("""
                <?xml version="1.0" encoding="UTF-8"?>
                <rendertheme version="6" xmlns="http://mapsforge.org/renderTheme">
                    <rule cat="places" e="any" zoom-min="10" zoom-max="16">
                        <rule e="node" k="place" v="city">
                            <circle fill="#FF0000" radius="10" />
                        </rule>
                        <rule e="any" k="*" v="*" zoom-min="12">
                            <caption fill="#0000FF" font-size="12" k="name" />
                        </rule>
                    </rule>
                    <rule e="any" k="highway" v="road">
                        <line stroke="#00FF00" stroke-width="2" />
                    </rule>
                </rendertheme>
                """);

            try
            {
                var theme = await LoadTheme(path);

                Assert.Equal(3, theme.Layers.Count);

                // Circle: inherits zoom-min/max and cat from its rule chain
                var circle = theme.Layers.OfType<MFCircleLayerStyle>().Single();

                Assert.Equal("circle1", circle.Name);
                Assert.Equal(10, circle.MinZoom);
                // zoom-max is inclusive in the theme, the layer bound is exclusive
                Assert.Equal(17, circle.MaxZoom);
                Assert.Equal("places", circle.Cat);
                Assert.Equal(10f, circle.Radius);
                Assert.Equal(new Color(255, 0, 0, 255), circle.Fill);

                // Caption: zoom-min of the parent and of its own rule combine
                var caption = theme.Layers.OfType<MFCaptionLayerStyle>().Single();

                Assert.Equal("caption2", caption.Name);
                Assert.Equal(12, caption.MinZoom);
                Assert.Equal(17, caption.MaxZoom);
                Assert.Equal("places", caption.Cat);
                Assert.Equal("name", caption.Key);
                Assert.Equal(12f, caption.FontSize);
                Assert.Equal(256, (int)theme.Layers.OfType<MFLineLayerStyle>().Single().MaxZoom);

                // Line without rule attributes: global zoom range, no category
                var line = theme.Layers.OfType<MFLineLayerStyle>().Single();

                Assert.Equal("line3", line.Name);
                Assert.Equal(0, line.MinZoom);
                Assert.Equal(string.Empty, line.Cat);
                Assert.Equal(2f, line.StrokeWidth);
                Assert.Equal(new Color(0, 255, 0, 255), line.Stroke);
            }
            finally
            {
                cleanup();
            }
        }

        /// <summary>
        /// Checks that a caption without a symbol reference of its rule is
        /// positioned against them (nearest symbol of the rule).
        /// </summary>
        [Fact]
        public async Task LoadRuleCaptionLinksToNearestSymbolTest()
        {
            var (path, cleanup) = WriteTheme("""
                <?xml version="1.0" encoding="UTF-8"?>
                <rendertheme version="6" xmlns="http://mapsforge.org/renderTheme">
                    <rule e="node" k="amenity" v="hospital">
                        <symbol id="sym" src="symbols/dot_black.svg" />
                        <caption fill="#000000" font-size="12" k="name" position="above" symbol-id="sym" />
                        <caption fill="#111111" font-size="12" k="alt_name" position="below" />
                        <caption fill="#222222" font-size="12" k="other_name" />
                    </rule>
                </rendertheme>
                """);

            try
            {
                var theme = await LoadTheme(path);

                // One symbol layer and three caption layers
                Assert.Equal(4, theme.Layers.Count);

                var referenced = theme.Layers.OfType<MFCaptionLayerStyle>().First(l => l.SymbolId == "sym");

                Assert.NotNull(referenced.PositionSymbol);
                Assert.Equal("symbols/dot_black.svg", referenced.PositionSymbolSrc);

                // Below: falls back to the first symbol of the rule
                var below = theme.Layers.OfType<MFCaptionLayerStyle>().First(l => l.Position == MapsforgePosition.Below);

                Assert.Equal(MapsforgePosition.Below, below.Position);
                Assert.NotNull(below.PositionSymbol);

                // Without position and symbol reference: no position symbol
                var plain = theme.Layers.OfType<MFCaptionLayerStyle>().First(l => l.Position == MapsforgePosition.Auto);

                Assert.Null(plain.PositionSymbol);
            }
            finally
            {
                cleanup();
            }
        }

        /// <summary>
        /// Checks line attributes: dash array handling (odd lengths are
        /// completed mapsforge style), cap and join.
        /// </summary>
        [Fact]
        public async Task LoadLineAttributesTest()
        {
            var (path, cleanup) = WriteTheme("""
                <?xml version="1.0" encoding="UTF-8"?>
                <rendertheme version="6" xmlns="http://mapsforge.org/renderTheme">
                    <rule e="way" k="highway" v="road">
                        <line stroke="#FF0000" stroke-width="2" stroke-dasharray="5,4,2"
                              stroke-linecap="round" stroke-linejoin="bevel" />
                        <line stroke="#00FF00" />
                        <line stroke="#0000FF" stroke-dasharray="10" />
                        <line />
                    </rule>
                </rendertheme>
                """);

            try
            {
                var theme = await LoadTheme(path);

                var lines = theme.Layers.OfType<MFLineLayerStyle>().ToList();
                Assert.Equal(4, lines.Count);

                // ["5,4,2" has an odd length: the first length is appended]
                Assert.Equal(new[] { 5f, 4f, 2f, 5f }, lines[0].StrokeDasharray);
                Assert.Equal(StrokeCap.Round, lines[0].Cap);
                Assert.Equal(StrokeJoin.Bevel, lines[0].Join);

                Assert.Equal(new Color(0, 255, 0, 255), lines[1].Stroke);
                Assert.Null(lines[1].StrokeDasharray);

                // Single dash length: also completed with itself
                Assert.Equal(new[] { 10f, 10f }, lines[2].StrokeDasharray);

                // A line without attributes uses the defaults
                Assert.Equal(new Color(0, 0, 0, 255), lines[3].Stroke); // default
                Assert.Equal(0f, lines[3].StrokeWidth);
                Assert.Null(lines[3].StrokeDasharray);
                Assert.Equal(StrokeCap.Round, lines[3].Cap); // mapsforge default
                Assert.Equal(StrokeJoin.Miter, lines[3].Join);
            }
            finally
            {
                cleanup();
            }
        }

        /// <summary>
        /// Checks the theme wide base factors scale the layers.
        /// </summary>
        [Fact]
        public async Task LoadBaseAttributesTest()
        {
            var (path, cleanup) = WriteTheme("""
                <?xml version="1.0" encoding="UTF-8"?>
                <rendertheme version="6" base-stroke-width="2" base-text-size="1.5"
                             xmlns="http://mapsforge.org/renderTheme">
                    <rule e="way" k="highway" v="road">
                        <line stroke="#FF0000" stroke-width="2" />
                    </rule>
                    <rule e="node" k="place" v="city">
                        <caption fill="#000000" font-size="12" k="name" />
                    </rule>
                </rendertheme>
                """);

            try
            {
                var theme = await LoadTheme(path);

                var line = theme.Layers.OfType<MFLineLayerStyle>().Single();
                var caption = theme.Layers.OfType<MFCaptionLayerStyle>().Single();

                Assert.Equal(2f, line.BaseStrokeWidth);
                Assert.Equal(1.5f, line.BaseTextSize);
                Assert.Equal(1.5f, caption.BaseTextSize);
            }
            finally
            {
                cleanup();
            }
        }

        /// <summary>
        /// Checks that element and closed conditions of rules translate into
        /// the geometry conditions of the layer filters.
        /// </summary>
        [Fact]
        public async Task LoadElementAndClosedConditionsTest()
        {
            var (path, cleanup) = WriteTheme("""
                <?xml version="1.0" encoding="UTF-8"?>
                <rendertheme version="6" xmlns="http://mapsforge.org/renderTheme">
                    <rule e="any" k="natural" v="*">
                        <rule e="any" closed="yes" k="*" v="*">
                            <area fill="#00FF00" />
                        </rule>
                        <rule e="any" closed="no" k="*" v="*">
                            <line stroke="#FF0000" />
                        </rule>
                        <rule e="node" k="*" v="*">
                            <circle fill="#0000FF" radius="5" />
                        </rule>
                    </rule>
                </rendertheme>
                """);

            try
            {
                var theme = await LoadTheme(path);

                Assert.Equal(3, theme.Layers.Count);

                var polygon = new Polygon(new LinearRing([new Coordinate(0, 0), new Coordinate(1, 0),
                    new Coordinate(1, 1), new Coordinate(0, 0)]));
                var line = new LineString([new Coordinate(0, 0), new Coordinate(1, 1)]);
                var point = new Point(0.5, 0.5);

                Feature Features(Geometry geometry, string tag)
                {
                    return new Feature(geometry, new NetTopologySuite.Features.AttributesTable { { "natural", tag } });
                }

                // Area only applies to polygons
                var area = theme.Layers.OfType<MFFillLayerStyle>().Single();

                Assert.Equal(true, area.Filter.Evaluate(new EvaluationContext(5, feature: Features(polygon, "wood"))));
                Assert.Equal(false, area.Filter.Evaluate(new EvaluationContext(5, feature: Features(line, "wood"))));

                // Line only applies to linear geometries
                var lineLayer = theme.Layers.OfType<MFLineLayerStyle>().Single();

                Assert.Equal(true, lineLayer.Filter.Evaluate(new EvaluationContext(5, feature: Features(line, "wood"))));
                Assert.Equal(false, lineLayer.Filter.Evaluate(new EvaluationContext(5, feature: Features(polygon, "wood"))));

                // Circle only applies to nodes
                var circle = theme.Layers.OfType<MFCircleLayerStyle>().Single();

                Assert.Equal(true, circle.Filter.Evaluate(new EvaluationContext(5, feature: Features(point, "peak"))));
                Assert.Equal(false, circle.Filter.Evaluate(new EvaluationContext(5, feature: Features(line, "peak"))));
            }
            finally
            {
                cleanup();
            }
        }

        // --- stroke caps ---

        /// <summary>
        /// Mapsforge theme spec: stroke-linecap defaults to "round" (see
        /// mapsforge's Line.parseCap), so a <line> without the attribute
        /// must render with round caps instead of butt.
        /// </summary>
        [Fact]
        public async Task LineWithoutLineCapDefaultsToRoundTest()
        {
            var (path, cleanup) = WriteTheme(
                """
                <?xml version="1.0" encoding="UTF-8"?>
                <rendertheme xmlns="http://opensciencemap.org/rendertheme" version="5">
                    <rule e="way" k="highway" v="*">
                        <line stroke="#FF0000" stroke-width="1.0" />
                    </rule>
                </rendertheme>
                """);

            try
            {
                var theme = await LoadTheme(path);
                var line = Assert.IsType<MFLineLayerStyle>(theme.Layers.Single());

                Assert.Equal(StrokeCap.Round, line.Cap);
            }
            finally
            {
                cleanup();
            }
        }

        /// <summary>
        /// An explicit stroke-linecap="butt" must still be honored.
        /// </summary>
        [Fact]
        public async Task LineWithExplicitButtCapTest()
        {
            var (path, cleanup) = WriteTheme(
                """
                <?xml version="1.0" encoding="UTF-8"?>
                <rendertheme xmlns="http://opensciencemap.org/rendertheme" version="5">
                    <rule e="way" k="highway" v="*">
                        <line stroke="#FF0000" stroke-width="1.0" stroke-linecap="butt" />
                    </rule>
                </rendertheme>
                """);

            try
            {
                var theme = await LoadTheme(path);
                var line = Assert.IsType<MFLineLayerStyle>(theme.Layers.Single());

                Assert.Equal(StrokeCap.Butt, line.Cap);
            }
            finally
            {
                cleanup();
            }
        }
    }
}
