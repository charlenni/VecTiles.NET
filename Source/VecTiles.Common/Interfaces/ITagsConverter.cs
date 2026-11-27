using NetTopologySuite.Features;
using NetTopologySuite.Geometries;

namespace VecTiles.Common.Interfaces;

/// <summary>
/// ITagsConverter converts the tags and the source layers of a feature to the tag and layer
/// conventions of a map style. It is only needed by style flavours that organize their features
/// in source layers derived from the tags (e.g. the MapLibre schema, where e.g. a feature
/// tagged 'building=yes' belongs to the source layer 'building'): the source layer of the raw
/// tile data cannot be derived from the data itself, the consumer of the converted vector tile
/// defines the mapping. Other style flavours render the features of the raw data directly and
/// do not need this conversion, they use the identity conversion of
/// <see cref="IdentityTagsConverter"/> which returns its input.
/// </summary>
public interface ITagsConverter
{
    /// <summary>
    /// Determines the source layer or layers of a feature from its tags and converts the tags of
    /// the feature to the attribute names and values expected by the style at those source
    /// layers. Styles may show one feature in several source layers (the MapLibre schema
    /// lists street names as separate features in the 'transportation_name' layer besides the
    /// road in the 'transportation' layer), hence the conversion may return more than one layer.
    /// </summary>
    /// <param name="geometry">Geometry of the feature, in the normalized tile space</param>
    /// <param name="sourceLayer">Provisional source layer of the feature (string.Empty when the
    /// raw data has no notion of source layers)</param>
    /// <param name="tags">Raw tags of the feature</param>
    /// <returns>Source layers of the feature and its tags converted per source layer; an empty
    /// feature is dropped when the result is empty</returns>
    (string SourceLayer, IAttributesTable Attributes)[] ConvertTags(Geometry geometry, string sourceLayer, IAttributesTable tags);
}
