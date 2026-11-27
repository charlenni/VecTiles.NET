using System.Text.Json;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using VecTiles.Common.Primitives;
using VecTiles.Styles.MapLibre;
using VecTiles.Styles.MapLibre.Json;
using VecTiles.Tests;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests for the sprite property of style files, which is either a single
/// sprite URL (Mapbox style spec) or a list of id/URL pairs (MapLibre style
/// spec, e.g. the VersaTiles styles), and for resolving the "id:name"
/// references of multi-sprite styles.
/// </summary>
public class SpriteDefinitionTests
{
    /// <summary>
    /// Checks that a single sprite URL (Mapbox style spec) is read as one
    /// unprefixed definition.
    /// </summary>
    [Fact]
    public void SpritePropertyAsStringIsParsedTest()
    {
        var styleFile = DeserializeStyle("""{ "sprite": "sprites/osm-liberty" }""");

        var definition = Assert.Single(styleFile.SpriteDefinitions);
        Assert.Null(definition.Id);
        Assert.Equal("sprites/osm-liberty", definition.Url);
    }

    /// <summary>
    /// Checks that a sprite list (MapLibre style spec) is read as id/URL pairs.
    /// </summary>
    [Fact]
    public void SpritePropertyAsArrayIsParsedTest()
    {
        var json = """
            {
                "sprite": [
                    { "id": "base", "url": "sprites/base" },
                    { "id": "extra", "url": "sprites/extra" }
                ]
            }
            """;

        var styleFile = DeserializeStyle(json);

        Assert.Equal(2, styleFile.SpriteDefinitions.Count);
        Assert.Equal("base", styleFile.SpriteDefinitions[0].Id);
        Assert.Equal("sprites/base", styleFile.SpriteDefinitions[0].Url);
        Assert.Equal("extra", styleFile.SpriteDefinitions[1].Id);
        Assert.Equal("sprites/extra", styleFile.SpriteDefinitions[1].Url);
    }

    /// <summary>
    /// Checks that loading multiple sprite definitions merges the atlases and
    /// registers the sprites of each definition under its "id:name" as well as
    /// its plain name.
    /// </summary>
    [Fact]
    public async Task LoadSpritesRegistersIdPrefixedNamesTest()
    {
        var definitions = new OMTSpriteDefinitionCollection
        {
            new SpriteDefinition { Id = "base", Url = "embedded://Sprites/osm-liberty" },
            new SpriteDefinition { Id = "extra", Url = "embedded://Sprites/osm-liberty" }
        };

        var sprites = await StyleFileLoader.LoadSprites(definitions);

        // 244 sprites per atlas, registered under their plain name (first
        // definition wins for duplicated names) and each definition's id
        Assert.Equal(244 * 3, sprites.Sprites.Count);
        Assert.Same(sprites.Sprites["base:airport_11"], sprites.Sprites["airport_11"]);

        // The second definition's sprites are distinct objects with equal content
        Assert.Equal(sprites.Sprites["base:airport_11"].Width, sprites.Sprites["extra:airport_11"].Width);
    }

    /// <summary>
    /// Checks that a symbol layer with a MapLibre style "id:name" icon-image
    /// resolves its sprite, and that an unknown id falls back to the plain
    /// sprite name.
    /// </summary>
    [Fact]
    public async Task SymbolFactoryResolvesIdPrefixedIconImageTest()
    {
        var definitions = new OMTSpriteDefinitionCollection
        {
            new SpriteDefinition { Id = "base", Url = "embedded://Sprites/osm-liberty" }
        };
        var sprites = await StyleFileLoader.LoadSprites(definitions);
        var factory = new SymbolFactory(sprites);

        // "base:airport_11" resolves through the registered id name
        var symbol = CreateIconSymbol(factory, "base:airport_11");
        Assert.NotNull(symbol);
        Assert.True(symbol.HasIcon);

        // An unknown id falls back to the plain sprite name
        var fallbackSymbol = CreateIconSymbol(factory, "unknown:airport_11");
        Assert.NotNull(fallbackSymbol);
        Assert.True(fallbackSymbol.HasIcon);
    }

    private static StyleFile DeserializeStyle(string json)
        => JsonSerializer.Deserialize<StyleFile>(json, StyleJson.Options)!;

    private static PointSymbol? CreateIconSymbol(SymbolFactory factory, string iconImage)
    {
        var layer = JsonSerializer.Deserialize<LayerStyle>(
            $$"""
            {
                "id": "test",
                "type": "symbol",
                "layout": { "icon-image": "{{iconImage}}" }
            }
            """, StyleJson.Options)!;

        var feature = new Feature(new Point(100, 100), new AttributesTable());
        var context = new EvaluationContext(14).WithFeature(feature);

        return factory.CreateSymbol(new Tile(0, 0, 4), layer, context, feature) as PointSymbol;
    }
}
