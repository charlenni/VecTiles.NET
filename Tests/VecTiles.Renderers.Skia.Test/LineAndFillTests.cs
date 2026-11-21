using SkiaSharp;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Converters.OpenMapTiles;
using VecTiles.DataSources.MbTiles;
using VecTiles.Renderers.Common;
using VecTiles.Styles.OpenMapTiles;
using VecTiles.TileDataSources;

namespace VecTiles.Renderers.Skia.Test;

public class LineAndFillTests
{
    private static readonly string _path = Utilities.DataFiles;
    private readonly string _dataFile = Path.Combine(_path, "zurich.mbtiles");
    private readonly string _styleFile = Path.Combine(_path, "osm-liberty.json");
    private readonly Renderer _renderer;

    public LineAndFillTests()
    {
        var dataSource = new MbTilesTileDataSource(_dataFile, determineZoomLevelsFromTilesTable: true, determineTileRangeFromTilesTable: true);
        var converter = new OMTTileConverter();
        var vectorDataSource = new VectorTileDataSource(dataSource, converter);

        var styleSource = $"file://{_path}/osm-liberty.json";
        var styleFile = OMTStyleFileLoader.Load(styleSource).Result;

        var spriteDictionary = styleFile.Sprites;
        var paintFactory = new OMTPaintFactory(spriteDictionary);
        var symbolFactory = new OMTSymbolFactory(spriteDictionary);
        var renderFactory = new RenderFactory(styleFile.Layers, paintFactory, symbolFactory);

        _renderer = new Renderer([new KeyValuePair<string, ITileDataSource>(vectorDataSource.Name, vectorDataSource)],  styleFile.Layers, renderFactory);
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
            var renderedTile = (RenderedTile?)(await _renderer.Render(tile));
            var renderContext = new EvaluationContext(tile.Zoom);

            using var bitmap = Utilities.CreateBitmap(512, 512);
            using (var canvas = new SKCanvas(bitmap))
            {
                renderedTile?.Draw(canvas, renderContext);
            }

            Utilities.SaveAndDestroyBitmap(bitmap, Path.Combine(Utilities.TestFiles, $"Tile-{x}x-{y}y-{zoom}z.png"));
        }
        catch (Exception e)
        {
            System.Diagnostics.Debug.WriteLine($"Tile {tile} couldn't be rendered");
        }
    }
}
