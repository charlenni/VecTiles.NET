using NetTopologySuite.Features;
using VecTiles.Common.Primitives;
using VecTiles.Styles.OpenMapTiles.Extensions;

namespace VecTiles.Styles.MapboxTests
{
    public class StringExtensionsTests
    {
        [Fact]
        public void ReplaceWithTagsReplacesAllTokens()
        {
            var feature = new Feature(null, new AttributesTable { { "name", "Kiel" }, { "name_regionalsprache", "Kiel (Plattdeutsch)" } });
            var context = new EvaluationContext(10, feature: feature);

            var result = "{name}\u000a{name_regionalsprache}".ReplaceWithTags(context);

            Assert.Equal("Kiel\u000aKiel (Plattdeutsch)", result);
        }

        [Fact]
        public void ReplaceWithTagsRemovesMissingNameTokens()
        {
            var feature = new Feature(null, new AttributesTable { { "name", "Kiel" } });
            var context = new EvaluationContext(10, feature: feature);

            var result = "{name}\u000a{name_regionalsprache}".ReplaceWithTags(context);

            Assert.Equal("Kiel\u000a", result);
        }

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
