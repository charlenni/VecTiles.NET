using BenchmarkDotNet.Attributes;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.DataSources.MbTiles;

namespace VecTiles.Converters.Mapbox.Benchmarks;

[MemoryDiagnoser]
public class MapboxConverterBenchmarks
{
    readonly string _path = "files\\zurich.mbtiles";

    ITileConverter? _tileConverter;
    List<Tile> _tiles = new List<Tile> { new Tile(1072, 717, 11), new Tile(8580, 5738, 14), new Tile(8581, 5738, 14), new Tile(8580, 5739, 14) };
    List<byte[]?> _data = new List<byte[]?>();

    [GlobalSetup]
    public void Setup()
    {
        var dataSource = new MbTilesTileDataSource(_path);

        _tileConverter = new MapboxTileConverter();

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
