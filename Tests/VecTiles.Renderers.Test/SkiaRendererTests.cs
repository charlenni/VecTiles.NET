using SkiaSharp;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Converters.OpenMapTiles;
using VecTiles.DataSources.MbTiles;
using VecTiles.Renderers.Common;
using VecTiles.Renderers.Skia;
using VecTiles.Styles.OpenMapTiles;
using VecTiles.TileDataSources;

namespace VecTiles.Renderers.Test;

public class SkiaRendererTests
{
    private const string Folder = "files";
    private readonly string _dataFile = Path.Combine(Folder, "zurich.mbtiles");
    private readonly string _styleFile = Path.Combine(Folder, "osm-liberty.json");
    private readonly Renderer _renderer;

    public SkiaRendererTests()
    {
        var dataSource = new MbTilesTileDataSource(_dataFile, determineZoomLevelsFromTilesTable: true, determineTileRangeFromTilesTable: true);
        var converter = new OMTTileConverter();
        var vectorDataSource = new VectorTileDataSource(dataSource, converter);

        var omtStyleStream = File.Open(_styleFile, FileMode.Open, FileAccess.Read);
        var omtStyleFile = OMTStyleFileLoader.Load(omtStyleStream).Result;

        var spriteDictionary = omtStyleFile.Sprites;
        var paintFactory = new OMTPaintFactory(spriteDictionary);
        var symbolFactory = new OMTSymbolFactory(spriteDictionary);
        var renderFactory = new RenderFactory(omtStyleFile.Layers, paintFactory, symbolFactory);

        _renderer = new Renderer([new KeyValuePair<string, ITileDataSource>(vectorDataSource.Name, vectorDataSource)],  omtStyleFile.Layers, renderFactory);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(1, 0, 1)]
    [InlineData(2, 1, 2)]
    [InlineData(4, 2, 3)]
    [InlineData(8, 5, 4)]
    [InlineData(16, 11, 5)]
    [InlineData(33, 22, 6)]
    [InlineData(67, 44, 7)]
    [InlineData(134, 89, 8)]
    [InlineData(268, 179, 9)]
    [InlineData(536, 358, 10)]
    [InlineData(1072, 717, 11)]
    [InlineData(2145, 1434, 12)]
    [InlineData(4290, 2869, 13)]
    [InlineData(8580, 5738, 14)]
    [InlineData(17161, 11476, 15)]
    [InlineData(34323, 22952, 16)]
    [InlineData(68646, 45904, 17)]
    [InlineData(137293, 91809, 18)]
    [InlineData(274587, 183619, 19)]
    [InlineData(549174, 367239, 20)]
    public async Task CreateTileTest(int x, int y, int zoom)
    {
        var tile = new Tile(x, y, zoom);

        try
        {
            var renderedTile = (RenderedTile)(await _renderer.Render(tile));
            var renderContext = new EvaluationContext(tile.Zoom);

            var imageInfo = new SKImageInfo {Width = 512, Height = 512, ColorType = SKColorType.Rgba8888, AlphaType = SKAlphaType.Premul};

            using var bitmap = new SKBitmap(imageInfo);
            using (var canvas = new SKCanvas(bitmap))
            {
                renderedTile?.Draw(canvas, renderContext);
            }

            using (var image = SKImage.FromBitmap(bitmap))
            using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
            await using (var stream = File.OpenWrite(Path.Combine(Folder, $"Tile-{x}x-{y}y-{zoom}z.png")))
            {
                data.SaveTo(stream);
            }
        }
        catch (Exception e)
        {
            System.Diagnostics.Debug.WriteLine($"Tile {tile} couldn't be rendered");
        }
    }
}