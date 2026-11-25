using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using ProtoBuf;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Converters.OpenMapTiles.Pbf;
using OmtParser = VecTiles.Converters.OpenMapTiles.Parser.Parser;

namespace VecTiles.Converters.Tests;

/// <summary>
/// Unit tests for <see cref="IdentityTagsConverter"/> and its identity fast path in
/// <see cref="OmtParser.Parse">OmtParser.Parse</see>.
/// </summary>
public class IdentityTagsConverterTests
{
    private static Geometry Geometry() => new Point(1, 1);

    private static IAttributesTable Tags() => new AttributesTable { { "highway", "secondary" } };

    private static void AssertIdentity((string SourceLayer, IAttributesTable Attributes)[] result, string expectedSourceLayer)
    {
        var (sourceLayer, attributes) = Assert.Single(result);
        Assert.Equal(expectedSourceLayer, sourceLayer);

        // The identity conversion keeps the tags of the feature unchanged
        Assert.Same(TagsTable, attributes);
    }

    private static IAttributesTable TagsTable { get; } = Tags();

    /// <summary>
    /// The strict identity conversion keeps an existing source layer of a feature unchanged.
    /// </summary>
    [Fact]
    public void KeepsExistingSourceLayer()
    {
        var result = IdentityTagsConverter.Instance.ConvertTags(Geometry(), "transportation", TagsTable);

        AssertIdentity(result, "transportation");
    }

    /// <summary>
    /// The strict identity conversion keeps an empty source layer (such features are dropped
    /// by the tile converters, the converter does not invent a name).
    /// </summary>
    [Fact]
    public void KeepsEmptySourceLayerWithoutFallback()
    {
        var result = IdentityTagsConverter.Instance.ConvertTags(Geometry(), string.Empty, TagsTable);

        AssertIdentity(result, string.Empty);
    }

    /// <summary>
    /// With a fallback source layer, features without a source layer of their own are assigned
    /// to the fallback layer.
    /// </summary>
    [Fact]
    public void AppliesFallbackToEmptySourceLayer()
    {
        var converter = new IdentityTagsConverter("mapsforge");

        var result = converter.ConvertTags(Geometry(), string.Empty, TagsTable);

        AssertIdentity(result, "mapsforge");
    }

    /// <summary>
    /// With a fallback source layer, an existing source layer of a feature is still kept
    /// unchanged.
    /// </summary>
    [Fact]
    public void KeepsExistingSourceLayerWithFallback()
    {
        var converter = new IdentityTagsConverter("mapsforge");

        var result = converter.ConvertTags(Geometry(), "transportation", TagsTable);

        AssertIdentity(result, "transportation");
    }

    /// <summary>
    /// Serializes a pbf tile to its binary MVT representation.
    /// </summary>
    private static byte[] Serialize(PbfTile pbfTile)
    {
        using var stream = new MemoryStream();
        Serializer.Serialize(stream, pbfTile);
        return stream.ToArray();
    }

    private static PbfTile BuildPointTile(string layerName)
    {
        var layer = new PbfLayer { Name = layerName, Version = 2, Extent = 4096 };
        layer.Features.Add(new PbfFeature
        {
            Type = PbfGeomType.Point,

            // MoveTo (command 1, count 1) to the center of the layer
            Geometry = { 9, 2048, 2048 },
        });

        var tile = new PbfTile();
        tile.Layers.Add(layer);
        return tile;
    }

    /// <summary>
    /// Parsing with a strict identity tags converter yields the same tile as parsing without
    /// any tags converter: the features stay in the layers and with the tags of the data (the
    /// identity conversion does not re-wrap the features of the tile).
    /// </summary>
    [Fact]
    public void ParsesStrictIdentityLikeNoConverter()
    {
        var data = Serialize(BuildPointTile("poi"));
        var requestedTile = new Tile(0, 0, 14);

        var withoutConverter = OmtParser.Parse(data, requestedTile, requestedTile);
        var withIdentityConverter = OmtParser.Parse(
            data, requestedTile, requestedTile, IdentityTagsConverter.Instance);

        Assert.Equal(withoutConverter.TileId, withIdentityConverter.TileId);
        var layer = Assert.Single(withIdentityConverter.Layers);
        Assert.Equal("poi", layer.Name);
    }

    /// <summary>
    /// Parsing with an identity tags converter with fallback source layer keeps an existing
    /// source layer of the data with its features and tags (the fallback only applies to
    /// features without a source layer of their own).
    /// </summary>
    [Fact]
    public void ParsesKeepsExistingSourceLayerWithFallback()
    {
        var data = Serialize(BuildPointTile("poi"));
        var requestedTile = new Tile(0, 0, 14);

        var vectorTile = OmtParser.Parse(
            data, requestedTile, requestedTile, new IdentityTagsConverter("mapsforge"));

        // With the fallback, the source layer of the features is an "existing" one and is
        // therefore kept; the features are re-wrapped in fresh layers by the conversion
        Assert.Single(vectorTile.Layers);
        Assert.Equal("poi", vectorTile.Layers[0].Name);
        Assert.Single(vectorTile.Layers[0].Features);
    }
}
