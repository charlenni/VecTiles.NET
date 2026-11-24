using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using VecTiles.Common.Enums;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Styles.Mapsforge;
using VecTiles.Styles.Mapsforge.Enums;
using VecTiles.Styles.Mapsforge.Expressions;

using VecTiles.Tests;

namespace VecTiles.Styles.Mapsforge.Tests
{
    /// <summary>
    /// Tests for the MFSymbolFactory: the symbols created for the symbol,
    /// caption, lineSymbol and pathText layers of a Mapsforge render theme.
    /// </summary>
    public class MFSymbolFactoryTests
    {
        private static readonly MFSpriteFactory SpriteFactory = new($"file://{RepoData.MapsforgeFiles}");

        private static readonly Tile Tile = new(1, 2, 10);

        private static Feature PointFeature(AttributesTable attributes)
        {
            return new Feature(new Point(1.5, 2.5), attributes);
        }

        private static Feature LineFeature(AttributesTable attributes)
        {
            return new Feature(new LineString([new Coordinate(0, 0), new Coordinate(10, 10)]), attributes);
        }

        private static MFSymbolLayerStyle SymbolLayer(string src, MapsforgeDisplay display = MapsforgeDisplay.IfSpace,
            MapsforgePosition position = MapsforgePosition.Auto, int priority = 0,
            int? symbolWidth = null, int? symbolHeight = null, int? symbolPercent = null)
        {
            return new MFSymbolLayerStyle("symbol1", MFAllExpression.Empty, 0, 256,
                src, symbolWidth, symbolHeight, symbolPercent, position, priority, display,
                string.Empty, null)
            {
                Source = "mapsforge"
            };
        }

        private static MFCaptionLayerStyle CaptionLayer(string key = "name", MapsforgeDisplay display = MapsforgeDisplay.IfSpace,
            MapsforgePosition position = MapsforgePosition.Auto, int priority = 0, int fontSize = 12,
            MapsforgeTextTransform transform = MapsforgeTextTransform.None,
            MapsforgeFontFamily family = MapsforgeFontFamily.Default, MapsforgeFontStyle style = MapsforgeFontStyle.Normal,
            float dy = 0f)
        {
            return new MFCaptionLayerStyle("caption1", MFAllExpression.Empty, 0, 256,
                key, family, style, fontSize, new Color(0, 0, 0, 255), Colors.Empty, 0f,
                dy, position, priority, display, null, transform, null)
            {
                Source = "mapsforge"
            };
        }

        private static readonly AttributesTable Named = new() { { "name", "Test" }, { "id", "42" } };

        /// <summary>
        /// The point symbols of the factory are wrapped in a PointSymbol
        /// envelope (the form the symbol placement and draw paths consume,
        /// like for the OpenMapTiles symbol factory); the tests read the
        /// inner parts.
        /// </summary>
        private static IconPointSymbol? IconOf(ISymbol? symbol)
        {
            return (symbol as PointSymbol)?.IconSymbol;
        }

        private static TextPointSymbol? TextOf(ISymbol? symbol)
        {
            return (symbol as PointSymbol)?.TextSymbol;
        }

        // --- point icons ---

        [Fact]
        public void PointIconSymbolTest()
        {
            var factory = new MFSymbolFactory(SpriteFactory);
            var symbol = factory.CreateSymbol(Tile, SymbolLayer("symbols/dot_black.svg"), EvaluationContext.Empty, PointFeature(Named));

            Assert.NotNull(symbol);
            Assert.IsType<PointSymbol>(symbol);
            var icon = IconOf(symbol)!;

            Assert.Equal(42ul, icon.Id);
            Assert.Equal("Test", icon.Name);
            Assert.Same(SpriteFactory.GetSprite("symbols/dot_black.svg"), icon.Icon);

            // The icon must be drawn: the sprite already carries the display
            // size, so the draw scale multiplier is 1 (a scale of 0 collapses
            // the icon to an invisible point in the renderers)
            Assert.Equal(1f, icon.Scale);
        }

        [Fact]
        public void PointIconAnchorTest()
        {
            var factory = new MFSymbolFactory(SpriteFactory);

            var below = IconOf(factory.CreateSymbol(Tile, SymbolLayer("symbols/dot_black.svg",
                position: MapsforgePosition.Below), EvaluationContext.Empty, PointFeature(Named)));
            var auto = IconOf(factory.CreateSymbol(Tile, SymbolLayer("symbols/dot_black.svg",
                position: MapsforgePosition.Auto), EvaluationContext.Empty, PointFeature(Named)));
            var right = IconOf(factory.CreateSymbol(Tile, SymbolLayer("symbols/dot_black.svg",
                position: MapsforgePosition.Right), EvaluationContext.Empty, PointFeature(Named)));

            Assert.NotNull(below);
            Assert.Equal(0.5, below.Anchor.X, 5);
            Assert.Equal(0, below.Anchor.Y, 5);

            Assert.NotNull(auto);
            Assert.Equal(0.5, auto.Anchor.X, 5);
            Assert.Equal(0.5, auto.Anchor.Y, 5);

            Assert.NotNull(right);
            Assert.Equal(0, right.Anchor.X, 5);
            Assert.Equal(0.5, right.Anchor.Y, 5);
        }

        [Fact]
        public void PointIconDisplayBehaviorsTest()
        {
            var factory = new MFSymbolFactory(SpriteFactory);

            var always = IconOf(factory.CreateSymbol(Tile, SymbolLayer("symbols/dot_black.svg", display: MapsforgeDisplay.Always),
                EvaluationContext.Empty, PointFeature(Named)));
            var order = IconOf(factory.CreateSymbol(Tile, SymbolLayer("symbols/dot_black.svg", display: MapsforgeDisplay.Order),
                EvaluationContext.Empty, PointFeature(Named)));
            var ifSpace = IconOf(factory.CreateSymbol(Tile, SymbolLayer("symbols/dot_black.svg", display: MapsforgeDisplay.IfSpace),
                EvaluationContext.Empty, PointFeature(Named)));
            var never = factory.CreateSymbol(Tile, SymbolLayer("symbols/dot_black.svg", display: MapsforgeDisplay.Never),
                EvaluationContext.Empty, PointFeature(Named));

            Assert.NotNull(always);
            Assert.True(always.AllowOverlap);
            Assert.True(always.AllowOthers);

            Assert.NotNull(order);
            Assert.True(order.AllowOverlap);
            Assert.False(order.AllowOthers);

            Assert.NotNull(ifSpace);
            Assert.False(ifSpace.AllowOverlap);

            Assert.Null(never);
        }

        [Fact]
        public void PointIconPrioritySortOrderTest()
        {
            var factory = new MFSymbolFactory(SpriteFactory);

            var symbol = IconOf(factory.CreateSymbol(Tile, SymbolLayer("symbols/dot_black.svg", priority: 17),
                EvaluationContext.Empty, PointFeature(Named)))!;

            Assert.NotNull(symbol);
            Assert.Equal(-17, symbol.SortOrder);
        }

        [Fact]
        public void PointIconMissingSourceIsDroppedTest()
        {
            var factory = new MFSymbolFactory(SpriteFactory);

            var symbol = factory.CreateSymbol(Tile, SymbolLayer("symbols/does-not-exist.svg"),
                EvaluationContext.Empty, PointFeature(Named));

            Assert.Null(symbol);
        }

        // --- captions ---

        [Fact]
        public void CaptionSymbolTest()
        {
            var factory = new MFSymbolFactory(SpriteFactory);
            var layer = CaptionLayer();
            layer.BaseTextSize = 1.5f;

            var symbol = factory.CreateSymbol(Tile, layer, EvaluationContext.Empty, PointFeature(Named));

            Assert.NotNull(symbol);
            var text = TextOf(symbol)!;

            Assert.Equal("Test", text.Text);
            Assert.Contains("Open Sans Regular", text.FontNames);
            Assert.Equal(18f, text.FontSize!(EvaluationContext.Empty), 5); // 12 * 1.5

            // Layers without a symbol reference are centered on the point
            Assert.Equal(0.5, text.Anchor.X, 5);
            Assert.Equal(0.5, text.Anchor.Y, 5);
        }

        [Fact]
        public void CaptionTextTransformAndFontsTest()
        {
            var factory = new MFSymbolFactory(SpriteFactory);
            var layer = CaptionLayer(transform: MapsforgeTextTransform.Uppercase,
                family: MapsforgeFontFamily.SansSerif, style: MapsforgeFontStyle.Bold);

            var symbol = TextOf(factory.CreateSymbol(Tile, layer, EvaluationContext.Empty, PointFeature(Named)));

            Assert.NotNull(symbol);
            Assert.Equal("TEST", symbol.Text);
            Assert.Contains("Open Sans Bold", symbol.FontNames);
        }

        /// <summary>
        /// Checks the caption position: above the referenced icon, with the dy
        /// shift applied to the offset.
        /// </summary>
        [Fact]
        public void CaptionPositionAboveSymbolTest()
        {
            var factory = new MFSymbolFactory(SpriteFactory);

            var symbolLayer = SymbolLayer("symbols/dot_black.svg", symbolWidth: 20);
            var layer = CaptionLayer(position: MapsforgePosition.Above, dy: 4f);
            layer.PositionSymbol = symbolLayer;

            var symbol = TextOf(factory.CreateSymbol(Tile, layer, EvaluationContext.Empty, PointFeature(Named)));

            Assert.NotNull(symbol);
            Assert.Equal(0.5, symbol.Anchor.X, 5);
            Assert.Equal(1, symbol.Anchor.Y, 5);
            Assert.Equal(0, symbol.Offset.X, 5);
            Assert.Equal(-10 + 4, symbol.Offset.Y, 5); // half of the 20px icon, pushed up, plus dy
        }

        [Fact]
        public void CaptionWithoutTextIsDroppedTest()
        {
            var factory = new MFSymbolFactory(SpriteFactory);

            var symbol = factory.CreateSymbol(Tile, CaptionLayer(key: "missing"), EvaluationContext.Empty, PointFeature(Named));

            Assert.Null(symbol);
        }

        // --- line symbols ---

        [Fact]
        public void RepeatedLineIconTest()
        {
            var factory = new MFSymbolFactory(SpriteFactory);
            var layer = new MFLineSymbolLayerStyle("lineSymbol1", MFAllExpression.Empty, 0, 256,
                "symbols/dot_black.svg", null, null, null, repeat: true, repeatGap: 150f, repeatStart: 30f,
                rotate: true, "right", 3f, MapsforgePosition.Center, -10, MapsforgeDisplay.Order)
            {
                Source = "mapsforge"
            };

            var symbol = factory.CreateSymbol(Tile, layer, EvaluationContext.Empty, LineFeature(Named));

            Assert.NotNull(symbol);
            var line = Assert.IsType<LineSymbol>(symbol);

            Assert.Equal(150f, line.Spacing, 5);
            Assert.NotNull(line.IconSymbol);
            Assert.Null(line.TextSymbol);
            Assert.Equal(10, line.IconSymbol.SortOrder); // -(-10): higher priority first
            // The icon follows the path direction
            Assert.Equal(MapAlignment.Map, line.IconSymbol.RotationAlignment);
            Assert.Equal(3, line.IconSymbol.Offset.Y, 5);
        }

        [Fact]
        public void SingleCenteredLineIconTest()
        {
            var factory = new MFSymbolFactory(SpriteFactory);
            var layer = new MFLineSymbolLayerStyle("lineSymbol1", MFAllExpression.Empty, 0, 256,
                "symbols/dot_black.svg", null, null, null, repeat: false, repeatGap: 150f, repeatStart: 30f,
                rotate: true, "right", 0f, MapsforgePosition.Center, 0, MapsforgeDisplay.IfSpace)
            {
                Source = "mapsforge"
            };

            var symbol = factory.CreateSymbol(Tile, layer, EvaluationContext.Empty, LineFeature(Named)) as LineSymbol;

            Assert.NotNull(symbol);
            Assert.Equal(0f, symbol.Spacing, 5); // placed once at the center
        }

        // --- path text ---

        [Fact]
        public void PathTextSymbolTest()
        {
            var factory = new MFSymbolFactory(SpriteFactory);
            var layer = new MFPathTextLayerStyle("pathText1", MFAllExpression.Empty, 0, 256,
                "name", MapsforgeFontFamily.Serif, MapsforgeFontStyle.Bold, 14f,
                new Color(0, 0, 0, 255), new Color(255, 255, 255, 255), 2f,
                repeat: false, repeatGap: 200f, repeatStart: 30f, rotate: true, 2f, -8, MapsforgeDisplay.Order,
                "auto", MapsforgeTextTransform.None)
            {
                Source = "mapsforge"
            };

            var symbol = factory.CreateSymbol(Tile, layer, EvaluationContext.Empty, LineFeature(Named));

            Assert.NotNull(symbol);
            var line = Assert.IsType<LineSymbol>(symbol);

            Assert.Null(line.IconSymbol);
            Assert.NotNull(line.TextSymbol);
            Assert.Equal("Test", line.TextSymbol.Text);
            Assert.Equal(8, line.TextSymbol.SortOrder); // -(-8): higher priority first
            Assert.Equal(-2, line.TextSymbol.Offset.Y, 5); // dy shifts up from the path
            Assert.Equal(14f, line.TextSymbol.FontSize!(EvaluationContext.Empty), 5);
        }

        /// <summary>
        /// Checks that unsupported layer styles are rejected.
        /// </summary>
        [Fact]
        public void UnsupportedLayerStyleThrowsTest()
        {
            var line = new MFLineLayerStyle("line1", MFAllExpression.Empty, 0, 256,
                new Color(255, 0, 0, 255), 3f, null, Common.Enums.StrokeCap.Butt, Common.Enums.StrokeJoin.Miter, null, 0f);

            Assert.Throws<ArgumentException>(() =>
                new MFSymbolFactory(SpriteFactory).CreateSymbol(Tile, line, EvaluationContext.Empty, PointFeature(Named)));
        }
    }
}
