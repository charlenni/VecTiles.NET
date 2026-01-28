using BenchmarkDotNet.Attributes;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.DataSources.MbTiles;

namespace VecTiles.Converters.OpenMapTiles.Benchmarks;

[MemoryDiagnoser]
public class OMTConverterBenchmarks
{
    readonly string _path = "files\\zurich.mbtiles";

    ITileConverter? _tileConverter;
    List<Tile> _tiles = new List<Tile> { new Tile(1072, 1330, 11), new Tile(8580, 10645, 14), new Tile(8581, 10645, 14), new Tile(8580, 10644, 14) };
    List<byte[]?> _data = new List<byte[]?>();

    [GlobalSetup]
    public void Setup()
    {
        var dataSource = new MbTilesTileDataSource(_path);

        _tileConverter = new OMTTileConverter();

        foreach (var tile in _tiles)
            _data.Add(dataSource.GetTileAsync(tile).ConfigureAwait(false).GetAwaiter().GetResult());
    }

    [Benchmark]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(3)]
    public void ReadVectorTile(int i)
    {
        var data = _data[i];

        if (data != null)
        {
            _tileConverter?.Convert(_tiles[i], _tiles[i], data)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
        }
    }

    [Benchmark]
    public void ReadVectorTiles()
    {
        for (var i = 0; i < _tiles.Count(); i++)
        {
            var data = _data[i];

            if (data != null)
            {
                _tileConverter?.Convert(_tiles[i], _tiles[i], data)
                    .ConfigureAwait(false)
                    .GetAwaiter()
                    .GetResult();
            }
        }
    }
}
