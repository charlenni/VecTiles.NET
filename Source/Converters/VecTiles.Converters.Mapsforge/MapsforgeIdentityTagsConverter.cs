using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using VecTiles.Common.Interfaces;

namespace VecTiles.Converters.Mapsforge;

/// <summary>
/// Identity tags converter for Mapsforge tile data rendered with a Mapsforge render theme: it
/// passes the raw tags of a feature through unchanged and assigns all features of the tile to
/// the single source layer <c>mapsforge</c>, the source layer the layer styles created from a
/// Mapsforge render theme reference (see MFLayerStyle.SourceLayer).
/// </summary>
/// <remarks>
/// Mapsforge render theme rules select their features by tags, not by source layers, so the
/// conversion is the identity on the tags; only the source layer has to be named, because a
/// source layer must not be empty (the tile converter drops features of empty source layers).
/// </remarks>
public class MapsforgeIdentityTagsConverter : ITagsConverter
{
    /// <summary>
    /// Gets the shared instance of the identity tags converter.
    /// </summary>
    public static MapsforgeIdentityTagsConverter Instance { get; } = new();

    /// <inheritdoc />
    public (string SourceLayer, IAttributesTable Attributes)[] ConvertTags(Geometry geometry, string sourceLayer, IAttributesTable tags)
    {
        return [(string.IsNullOrEmpty(sourceLayer) ? "mapsforge" : sourceLayer, tags)];
    }
}
