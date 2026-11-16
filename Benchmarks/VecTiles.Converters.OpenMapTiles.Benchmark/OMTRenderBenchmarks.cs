using BenchmarkDotNet.Attributes;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.DataSources.MbTiles;
using VecTiles.Renderers.Common;
using VecTiles.Renderers.Common.Interfaces;
using VecTiles.Renderers.Skia;
using VecTiles.Styles.OpenMapTiles;

namespace VecTiles.Converters.OpenMapTiles.Benchmark;

[MemoryDiagnoser]
public class OMTRenderBenchmarks
{
    readonly string _path = Path.Combine("Files");

    private ITileDataSource? _dataSource;
    ITileConverter? _tileConverter;
    Renderer? _renderer;
    
    List<Tile> _tiles = new List<Tile> { 
        new Tile(67, 44, 7), 
        new Tile(536, 358, 10), 
        new Tile(4290, 2868, 13), 
        new Tile(8580, 5737, 14), 
        new Tile(137291, 91799, 18) 
    };

    private List<byte[]?> _data = [null, null, null, null];
    
    [GlobalSetup]
    public void Setup()
    {
        var stream = File.Open(Path.Combine(_path, "osm-liberty.json"), FileMode.Open, FileAccess.Read);
        var styleFile = OMTStyleFileLoader.Load(stream).Result;

        _dataSource = new MbTilesTileDataSource(Path.Combine(_path, "zurich.mbtiles"));
        _tileConverter = new OMTTileConverter();
        _renderer = new Renderer(
            styleFile.Sources.Select(s => 
                new KeyValuePair<string, ITileDataSource>(s.Key, (ITileDataSource)s.Value.DataSource)), 
            styleFile.Layers, 
            new RenderFactory(styleFile.Layers, 
                new OMTPaintFactory(styleFile.Sprites), 
                new OMTSymbolFactory(styleFile.Sprites)));
    }

    [Benchmark]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(3)]
    [Arguments(4)]
    public async Task<IRenderedTile?> RenderVectorTile(int i)
    {
        return await _renderer?.Render(_tiles[i]);
    }

    public async Task<IRenderedTile?> RenderVectorLayerTile(int i)
    {
        return await _renderer?.Render(_tiles[i]);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _renderer = null;
    }
}
