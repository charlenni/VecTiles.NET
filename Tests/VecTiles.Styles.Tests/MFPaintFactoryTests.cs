using VecTiles.Common.Enums;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Styles.Mapsforge.Expressions;

using VecTiles.Tests;

namespace VecTiles.Styles.Mapsforge.Tests
{
    /// <summary>
    /// Tests for the MFPaintFactory: paints created from the layer styles of
    /// an Mapsforge render theme.
    /// </summary>
    public class MFPaintFactoryTests
    {
        private static readonly MFSpriteFactory SpriteFactory = new($"file://{RepoData.MapsforgeFiles}");

        // --- background ---

        [Fact]
        public void BackgroundPaintTest()
        {
            var style = new MFBackgroundLayerStyle("background", new Color(0xF8, 0xF8, 0xF8, 255), new Color(1, 2, 3, 4));

            var paint = new MFPaintFactory(SpriteFactory).CreatePaint(style);
            paint.Update(EvaluationContext.Empty);

            Assert.NotNull(paint);
            Assert.Equal(PaintStyle.Fill, paint.Style);
            Assert.Equal(new Color(0xF8, 0xF8, 0xF8, 255), paint.Color);
            Assert.Null(paint.Pattern);
        }

        // --- line ---

        [Fact]
        public void LinePaintTest()
        {
            var line = new MFLineLayerStyle("line1", MFAllExpression.Empty, 0, 256,
                new Color(255, 0, 0, 255), 3f, [5f, 4f], StrokeCap.Round, StrokeJoin.Bevel, null, 0f);

            var paint = new MFPaintFactory(SpriteFactory).CreatePaint(line);
            paint.Update(EvaluationContext.Empty);

            Assert.NotNull(paint);
            Assert.Equal(PaintStyle.Stroke, paint.Style);
            Assert.Equal(new Color(255, 0, 0, 255), paint.Color);
            Assert.Equal(3f, paint.StrokeWidth, 5);
            Assert.Equal(StrokeCap.Round, paint.StrokeCap);
            Assert.Equal(StrokeJoin.Bevel, paint.StrokeJoin);
            Assert.Equal(new[] { 5f, 4f }, paint.DashArray);
        }

        /// <summary>
        /// Checks that the dash lengths and the stroke width are scaled with
        /// the base-stroke-width factor of the theme.
        /// </summary>
        [Fact]
        public void LinePaintBaseStrokeWidthTest()
        {
            var line = new MFLineLayerStyle("line1", MFAllExpression.Empty, 0, 256,
                new Color(255, 0, 0, 255), 3f, [5f, 4f], StrokeCap.Butt, StrokeJoin.Miter, null, 0f)
            {
                BaseStrokeWidth = 2f
            };

            var paint = new MFPaintFactory(SpriteFactory).CreatePaint(line);
            paint.Update(EvaluationContext.Empty);

            Assert.NotNull(paint);
            Assert.Equal(6f, paint.StrokeWidth, 5);
            Assert.Equal(new[] { 10f, 8f }, paint.DashArray);
        }

        /// <summary>
        /// Checks that the stroke width grows like in the original renderer
        /// with the zoom (1.5 per OSM zoom level above 12 — the tiles of the
        /// renderer are 512 px, so one level is added to the OSM zoom of the
        /// same pixel density), while the dash array stays at its base size.
        /// </summary>
        [Fact]
        public void LinePaintStrokeScaleTest()
        {
            var line = new MFLineLayerStyle("line1", MFAllExpression.Empty, 0, 256,
                new Color(255, 0, 0, 255), 3f, [5f, 4f], StrokeCap.Butt, StrokeJoin.Miter, null, 0f);

            var paint = new MFPaintFactory(SpriteFactory).CreatePaint(line);
            paint.Update(new EvaluationContext(14f));

            Assert.NotNull(paint);
            // 3 (theme) * 1.5 ^ (14 + 1 - 12) = 10.125
            Assert.Equal(10.125f, paint.StrokeWidth, 5);
            Assert.Equal(new[] { 5f, 4f }, paint.DashArray);
        }

        /// <summary>
        /// Checks that a line pattern replaces the stroke color.
        /// </summary>
        [Fact]
        public void LinePaintPatternTest()
        {
            var line = new MFLineLayerStyle("line1", MFAllExpression.Empty, 0, 256,
                new Color(255, 0, 0, 255), 3f, null, StrokeCap.Butt, StrokeJoin.Miter,
                "patterns/dot.png", 0f);

            var paint = new MFPaintFactory(SpriteFactory).CreatePaint(line);
            paint.Update(EvaluationContext.Empty);

            Assert.NotNull(paint);
            Assert.Equal(Colors.White, paint.Color);
            Assert.NotNull(paint.Pattern);
            Assert.True(paint.Pattern.Width > 0);
        }

        // --- area ---

        [Fact]
        public void AreaPaintTest()
        {
            var area = new MFFillLayerStyle("area1", MFAllExpression.Empty, 0, 256,
                new Color(0, 255, 0, 200), new Color(0, 0, 255, 255), 2f, null);

            var paint = new MFPaintFactory(SpriteFactory).CreatePaint(area);
            paint.Update(EvaluationContext.Empty);

            Assert.NotNull(paint);
            Assert.Equal(PaintStyle.Fill, paint.Style);
            Assert.Equal(new Color(0, 255, 0, 200), paint.Color);
            Assert.Equal(new Color(0, 0, 255, 255), paint.OutlineColor);
            Assert.Equal(2f, paint.StrokeWidth, 5);
            Assert.Null(paint.Pattern);
        }

        /// <summary>
        /// Checks that an area pattern replaces the fill color.
        /// </summary>
        [Fact]
        public void AreaPaintPatternTest()
        {
            var area = new MFFillLayerStyle("area1", MFAllExpression.Empty, 0, 256,
                new Color(0, 255, 0, 255), Colors.Empty, 0f, "patterns/dot.png");

            var paint = new MFPaintFactory(SpriteFactory).CreatePaint(area);
            paint.Update(EvaluationContext.Empty);

            Assert.NotNull(paint);
            Assert.Equal(Colors.White, paint.Color);
            Assert.NotNull(paint.Pattern);
        }

        // --- circle ---

        [Fact]
        public void CirclePaintTest()
        {
            var circle = new MFCircleLayerStyle("circle1", MFAllExpression.Empty, 0, 256,
                12f, new Color(255, 0, 0, 255), new Color(0, 0, 0, 255), 3f);

            var paint = new MFPaintFactory(SpriteFactory).CreatePaint(circle);
            paint.Update(EvaluationContext.Empty);

            Assert.NotNull(paint);
            Assert.Equal(PaintStyle.Fill, paint.Style);
            Assert.Equal(new Color(255, 0, 0, 255), paint.Color);
            Assert.Equal(new Color(0, 0, 0, 255), paint.OutlineColor);
            Assert.Equal(12f, paint.Radius, 5);
            Assert.Equal(3f, paint.StrokeWidth, 5);
        }

        // --- symbol ---

        /// <summary>
        /// Checks that symbol layers produce no paint (they produce symbols).
        /// </summary>
        [Fact]
        public void SymbolLayerGetNoPaintTest()
        {
            var symbol = new MFSymbolLayerStyle("symbol1", MFAllExpression.Empty, 0, 256,
                "symbols/dot_black.svg", null, null, null, Enums.MapsforgePosition.Auto, 0,
                Enums.MapsforgeDisplay.IfSpace, string.Empty, null)
            {
                Source = "mapsforge"
            };

            Assert.Null(new MFPaintFactory(SpriteFactory).CreatePaint(symbol));
        }

        /// <summary>
        /// Checks that unknown layer style types are rejected.
        /// </summary>
        [Fact]
        public void UnknownStyleTypeThrowsTest()
        {
            Assert.Throws<ArgumentException>(() =>
                new MFPaintFactory(SpriteFactory).CreatePaint(new UnknownStyleType()));
        }

        /// <summary>
        /// A layer style with an unknown style type.
        /// </summary>
        private class UnknownStyleType : ILayerStyle
        {
            public string Name => "unknown";

            public LayerStyleType StyleType => (LayerStyleType)999;

            public float MinZoom => 0;

            public float MaxZoom => -1;

            public bool Visible => true;

            public IExpression Filter => MFAllExpression.Empty;

            public string Source => "mapsforge";

            public string SourceLayer => "mapsforge";

            public void Update(EvaluationContext context)
            {
            }
        }
    }
}
