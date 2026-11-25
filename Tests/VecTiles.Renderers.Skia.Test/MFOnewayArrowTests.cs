using BruTile;
using VecTiles.Converters.Mapsforge;
using VecTiles.Controls.Mapsui;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Renderers.Common;
using VecTiles.Renderers.Common.Interfaces;

namespace VecTiles.Renderers.Skia.Test;

/// <summary>
/// Regression test of the oneway arrows of the Mapsforge pipeline (the
/// lineSymbol instruction of the render theme, an icon repeated along the
/// ways). Guards against the defect found on tile 19/274577/183589: the icons
/// were placed but drawn with the size zero, because the default scale of the
/// IconLineSymbol was 0 (the Mapsforge symbol factory does not set it, the
/// display size comes from the sprite itself).
/// </summary>
public class MFOnewayArrowTests
{
    private static readonly string MapFile = Path.Combine(Utilities.DataFiles, "..",
        "Mapsforge", "zuerich.map");
    private static readonly string MapsforgeTheme = Path.Combine(Utilities.DataFiles, "..",
        "Mapsforge", "mapsforge", "default.xml");

    [Fact]
    public async Task OnewayArrowsArePlacedWithTheirIconSizeTest()
    {
        using var tileSource = new MFRenderedTileSource(MapFile, new IdentityTagsConverter("mapsforge"), MapsforgeTheme);
        var tile = await tileSource.GetTileAsync(new TileInfo
        {
            Index = new TileIndex(274577, 183589, 19),
        });

        Assert.NotNull(tile);

        var baked = tile!.Layers.OfType<RenderedSymbolLayer>()
            .SelectMany(layer => layer.Symbols.Symbols)
            .OfType<LineSymbol>()
            .Where(symbol => symbol.IconSymbol is not null)
            .ToList();

        // The oneway rule of the theme created line symbols with an icon
        Assert.NotEmpty(baked);

        foreach (var symbol in baked)
        {
            Assert.Equal(1f, symbol.IconSymbol!.Scale);
        }
    }
}
