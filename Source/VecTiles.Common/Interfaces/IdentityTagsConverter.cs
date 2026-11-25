using NetTopologySuite.Features;
using NetTopologySuite.Geometries;

namespace VecTiles.Common.Interfaces;

/// <summary>
/// Identity tags converter: it keeps the raw tags of a feature unchanged and passes the
/// provisional source layer of the feature through. It is used by style flavours that render
/// the features of the raw tile data directly, either with the source layers of the data (the
/// OpenMapTiles schema already organizes the features in the source layers the styles expect)
/// or in a single source layer named by <see cref="IdentityTagsConverter(string)"/> (raw
/// Mapsforge data has no notion of source layers; a Mapsforge render theme references all
/// features as the source layer 'mapsforge', see MFLayerStyle.SourceLayer).
/// </summary>
public class IdentityTagsConverter : ITagsConverter
{
    /// <summary>
    /// Gets the shared instance of the strict identity tags converter: it keeps the source
    /// layer of a feature as delivered by the raw tile data.
    /// </summary>
    public static IdentityTagsConverter Instance { get; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="IdentityTagsConverter"/> class.
    /// </summary>
    /// <param name="fallbackSourceLayer">Source layer assigned to a feature whose raw tile
    /// data has no notion of source layers; without a fallback the empty source layer is kept
    /// (features with an empty source layer are dropped by the tile converters).</param>
    public IdentityTagsConverter(string? fallbackSourceLayer = null)
    {
        FallbackSourceLayer = fallbackSourceLayer;
    }

    /// <summary>
    /// Gets the source layer assigned to a feature without a source layer of its own, or null
    /// when the identity conversion keeps the (possibly empty) source layer unchanged.
    /// </summary>
    public string? FallbackSourceLayer { get; }

    /// <inheritdoc />
    public (string SourceLayer, IAttributesTable Attributes)[] ConvertTags(Geometry geometry, string sourceLayer, IAttributesTable tags)
    {
        return [(string.IsNullOrEmpty(sourceLayer) ? FallbackSourceLayer ?? sourceLayer : sourceLayer, tags)];
    }
}
