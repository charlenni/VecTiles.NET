using NetTopologySuite.Features;
using VecTiles.Common.Primitives;
using VecTiles.Styles.OpenMapTiles.Extensions;

namespace VecTiles.Styles.MapboxTests
{
    /// <summary>
    /// Tests for the ReplaceWithTags string extension.
    /// </summary>
    public class StringExtensionsTests
    {
        /// <summary>
        /// Verifies that ReplaceWithTags replaces all tokens with the feature attribute values.
        /// </summary>
        [Fact]
        public void ReplaceWithTagsReplacesAllTokens()
        {
            var feature = new Feature(null, new AttributesTable { { "name", "Kiel" }, { "name_regionalsprache", "Kiel (Plattdeutsch)" } });
            var context = new EvaluationContext(10, feature: feature);

            var result = "{name}\u000a{name_regionalsprache}".ReplaceWithTags(context);

            Assert.Equal("Kiel\u000aKiel (Plattdeutsch)", result);
        }

        /// <summary>
        /// Verifies that ReplaceWithTags removes tokens whose attribute is missing.
        /// </summary>
        [Fact]
        public void ReplaceWithTagsRemovesMissingNameTokens()
        {
            var feature = new Feature(null, new AttributesTable { { "name", "Kiel" } });
            var context = new EvaluationContext(10, feature: feature);

            var result = "{name}\u000a{name_regionalsprache}".ReplaceWithTags(context);

            Assert.Equal("Kiel\u000a", result);
        }

        /// <summary>
        /// Verifies that ReplaceWithTags leaves unknown tokens untouched.
        /// </summary>
        [Fact]
        public void ReplaceWithTagsLeavesUnknownTokens()
        {
            var feature = new Feature(null, new AttributesTable { { "name", "Kiel" } });
            var context = new EvaluationContext(10, feature: feature);

            var result = "{name} ({unknownfield})".ReplaceWithTags(context);

            Assert.Equal("Kiel ({unknownfield})", result);
        }
    }
}
