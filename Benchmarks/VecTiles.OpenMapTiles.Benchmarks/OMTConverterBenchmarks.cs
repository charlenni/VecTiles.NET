using BenchmarkDotNet.Attributes;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Converters.OpenMapTiles;
using VecTiles.DataSources.MbTiles;
using VecTiles.Renderers.Common;
using VecTiles.Styles.OpenMapTiles;

namespace VecTiles.OpenMapTiles.Benchmarks;

[MemoryDiagnoser]
public class OMTConverterBenchmarks
{
    private const string _path = "Files";

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
        var styleSource = $"file://{_path}osm-liberty.json";
        var styleFile = OMTStyleFileLoader.Load(styleSource).Result;

        _dataSource = new MbTilesTileDataSource(Path.Combine(_path, "zurich.mbtiles"));
        _tileConverter = new OMTTileConverter();
        
        for (var i = 0; i < _data.Count; i++)
        {
            _data[i] = _dataSource.GetTileAsync(_tiles[i]).GetAwaiter().GetResult();
        }
    }

    /*[Benchmark]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(3)]*/
    public async Task<byte[]?> ReadVectorTile(int i)
    {
        return await _dataSource.GetTileAsync(_tiles[i]);
    }

    [Benchmark]
    //[Arguments(0, 0)]
    //[Arguments(1, 1)]
    //[Arguments(2, 2)]
    [Arguments(3, 3)]
    [Arguments(3, 4)]
    public async Task<VectorTile?> ConvertVectorTile(int i, int j)
    {
        return await _tileConverter?.Convert(_tiles[j], _tiles[i], _data[i]);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
    }
}
