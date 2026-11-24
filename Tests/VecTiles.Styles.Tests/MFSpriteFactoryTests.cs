using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;

using VecTiles.Tests;

namespace VecTiles.Styles.Mapsforge.Tests
{
    /// <summary>
    /// Tests for the MFSpriteFactory: resolution of icon and pattern sources
    /// into raster (atlas) sprites and vector sprites.
    /// </summary>
    public class MFSpriteFactoryTests
    {
        private readonly string _base = $"file://{RepoData.MapsforgeFiles}";

        // --- SVG sprites ---

        /// <summary>
        /// Checks that an SVG source without size attributes becomes a vector
        /// sprite scaled to the mapsforge default size of 20 x 20 px, like
        /// mapsforge scales SVG symbols without symbol-width / symbol-height.
        /// </summary>
        [Fact]
        public void SvgSpriteIntrinsicSizeTest()
        {
            var sprite = new MFSpriteFactory(_base).GetSprite("symbols/dot_black.svg");

            Assert.NotNull(sprite);
            var vector = Assert.IsType<VectorSprite>(sprite);

            Assert.Equal(20, vector.Width); // 580 x 580 intrinsic, scaled to the 20 x 20 default
            Assert.Equal(20, vector.Height);
            Assert.Equal(580f, vector.IntrinsicWidth); // the intrinsic size is kept for the draw scale
            Assert.Equal(580f, vector.IntrinsicHeight);
            Assert.Equal(1f, vector.PixelRatio);
        }

        /// <summary>
        /// Checks that the vector binary of the sprite is the SVG document.
        /// </summary>
        [Fact]
        public void SvgSpriteCarriesSourceDataTest()
        {
            var data = File.ReadAllBytes(Path.Combine(RepoData.MapsforgeFiles, "symbols", "dot_black.svg"));

            var sprite = (VectorSprite)new MFSpriteFactory(_base).GetSprite("symbols/dot_black.svg")!;

            Assert.Equal(data, sprite.VectorBinary);
            Assert.NotNull(sprite.Atlas);
        }

        /// <summary>
        /// Checks the size attributes of a symbol source.
        /// </summary>
        [Fact]
        public void SvgSpriteSymbolSizesTest()
        {
            var factory = new MFSpriteFactory(_base);

            var widthOnly = factory.GetSprite("symbols/dot_black.svg", symbolWidth: 20)!;

            Assert.Equal(20, widthOnly.Width);
            Assert.Equal(20, widthOnly.Height); // square icon follows the aspect ratio

            var percent = factory.GetSprite("symbols/dot_black.svg", symbolPercent: 50)!;

            Assert.Equal(10, percent.Width); // 50% of the 20 x 20 default size
            Assert.Equal(10, percent.Height);
        }

        /// <summary>
        /// Checks that a symbol height alone scales the width with the aspect
        /// ratio of the intrinsic size.
        /// </summary>
        [Fact]
        public void SvgSpriteNonSquareAspectRatioTest()
        {
            var directory = Path.Combine(Path.GetTempPath(), "VecTilesSpriteFactoryTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);

            File.WriteAllText(Path.Combine(directory, "wide.svg"),
                "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 40 20\"></svg>");

            try
            {
                var sprite = new MFSpriteFactory($"file://{directory}").GetSprite("wide.svg", symbolHeight: 10)!;

                Assert.Equal(20, sprite.Width);
                Assert.Equal(10, sprite.Height);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        // --- raster sprites ---

        /// <summary>
        /// Checks that a PNG source becomes an atlas sprite of its own at the
        /// size given by its IHDR chunk.
        /// </summary>
        [Fact]
        public void PngSpriteTest()
        {
            var sprite = new MFSpriteFactory(_base).GetSprite("patterns/dot.png");

            Assert.NotNull(sprite);
            Assert.IsNotType<VectorSprite>(sprite);

            Assert.Equal(18, sprite.Width); // dot.png IHDR: 18x18
            Assert.Equal(18, sprite.Height);
        }

        /// <summary>
        /// Checks that a requested width scales a raster sprite.
        /// </summary>
        [Fact]
        public void PngSpriteScaledTest()
        {
            var sprite = new MFSpriteFactory(_base).GetSprite("patterns/dot.png", symbolWidth: 9)!;

            Assert.Equal(9, sprite.Width);
            Assert.Equal(9, sprite.Height);
        }

        // --- source resolution ---

        /// <summary>
        /// Checks that the "jar:" scheme of mapsforge themes is resolved
        /// against the base location.
        /// </summary>
        [Fact]
        public void JarSourceResolutionTest()
        {
            var sprite = new MFSpriteFactory(_base).GetSprite("jar:symbols/dot_black.svg");

            Assert.NotNull(sprite);
            Assert.IsType<VectorSprite>(sprite);
        }

        /// <summary>
        /// Checks that a "jar:" source which is not present relative to the
        /// base location falls back to the parent folder, so that sprites
        /// from a theme in a sub-folder (e.g. "mapsforge/default.xml")
        /// resolve against the shared "symbols" / "patterns" folders of the
        /// parent directory.
        /// </summary>
        [Fact]
        public void JarSourceParentFallbackTest()
        {
            var themeDirectory = Path.Combine(RepoData.MapsforgeFiles, "mapsforge");
            var sprite = new MFSpriteFactory($"file://{themeDirectory}").GetSprite("jar:symbols/bench.svg");

            Assert.NotNull(sprite);
            Assert.IsType<VectorSprite>(sprite);
        }

        /// <summary>
        /// Checks that a relative base URL or path ("file://Mapsforge/..." as
        /// an app builds it from a relative content path, or a plain relative
        /// path) does not break the sprite resolution: "file://name" must not
        /// turn "name" into a URI host. The factory normalizes the base to an
        /// absolute file URL of the working directory.
        /// </summary>
        [Fact]
        public void RelativeFileBaseResolutionTest()
        {
            var directory = Path.Combine(Path.GetTempPath(), "VecTilesSpriteFactoryTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);

            File.WriteAllText(Path.Combine(directory, "icon.svg"),
                "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"10\" height=\"10\"></svg>");

            var previousWorkingDirectory = Environment.CurrentDirectory;

            try
            {
                Environment.CurrentDirectory = directory;

                var relativeHost = new MFSpriteFactory("file://images").GetSprite("icon.svg");
                Assert.NotNull(relativeHost);

                var relativePath = new MFSpriteFactory("images").GetSprite("icon.svg");
                Assert.NotNull(relativePath);

                var absoluteUrl = new MFSpriteFactory($"file://{directory}").GetSprite("icon.svg");
                Assert.NotNull(absoluteUrl);
            }
            finally
            {
                Environment.CurrentDirectory = previousWorkingDirectory;
                Directory.Delete(directory, true);
            }
        }

        /// <summary>
        /// Checks that SVG data is detected from its content (and not only
        /// from the file extension).
        /// </summary>
        [Fact]
        public void SvgContentSniffingTest()
        {
            var directory = Path.Combine(Path.GetTempPath(), "VecTilesSpriteFactoryTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);

            File.WriteAllText(Path.Combine(directory, "icon"),
                "<?xml version=\"1.0\"?><svg xmlns=\"http://www.w3.org/2000/svg\" width=\"10\" height=\"10\"></svg>");

            try
            {
                var sprite = new MFSpriteFactory($"file://{directory}").GetSprite("icon");

                Assert.NotNull(sprite);
                Assert.IsType<VectorSprite>(sprite);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        /// <summary>
        /// Checks that a missing sprite source yields null instead of an
        /// exception (the theme must stay renderable).
        /// </summary>
        [Fact]
        public void MissingSourceYieldsNullTest()
        {
            Assert.Null(new MFSpriteFactory(_base).GetSprite("symbols/does-not-exist.svg"));
            Assert.Null(new MFSpriteFactory(_base).GetSprite(string.Empty));
            Assert.Null(new MFSpriteFactory(_base).GetSprite(null));
        }

        /// <summary>
        /// Checks that a malformed SVG document falls back to the default size.
        /// </summary>
        [Fact]
        public void MalformedSvgDefaultSizeTest()
        {
            var directory = Path.Combine(Path.GetTempPath(), "VecTilesSpriteFactoryTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);

            File.WriteAllText(Path.Combine(directory, "broken.svg"), "this is not an XML document");

            try
            {
                var sprite = new MFSpriteFactory($"file://{directory}").GetSprite("broken.svg")!;

                Assert.Equal(20, sprite.Width); // mapsforge default symbol size
                Assert.Equal(20, sprite.Height);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        /// <summary>
        /// Checks that sprites of the same source and size are shared.
        /// </summary>
        [Fact]
        public void SpriteCachingTest()
        {
            var factory = new MFSpriteFactory(_base);

            var first = factory.GetSprite("symbols/dot_black.svg", symbolWidth: 20);
            var second = factory.GetSprite("symbols/dot_black.svg", symbolWidth: 20);
            var other = factory.GetSprite("symbols/dot_black.svg", symbolWidth: 30);

            Assert.NotNull(first);
            Assert.Same(first, second);
            Assert.NotSame(first, other);
        }
    }
}
