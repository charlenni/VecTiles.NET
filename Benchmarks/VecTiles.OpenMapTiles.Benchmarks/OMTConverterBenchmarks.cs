using BenchmarkDotNet.Attributes;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Converters.OpenMapTiles;
using VecTiles.DataSources.MbTiles;
using VecTiles.Renderers.Common;
using VecTiles.Styles.OpenMapTiles;

namespace VecTiles.OpenMapTiles.Benchmarks;

/// <summary>
/// Benchmarks for the OpenMapTiles vector tile conversion: reads raw vector
/// tiles from an MBTiles data source at several zoom levels and converts them
/// with an <see cref="OMTTileConverter"/> into transcoded <c>VectorTile</c>s.
/// </summary>
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
    
    /// <summary>
    /// Loads the style file, creates the MBTiles data source and the tile
    /// converter and prefetches the raw tile data for all benchmark tiles.
    /// </summary>
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
    /// <summary>
    /// Reads the raw vector tile data of one benchmark tile from the MBTiles
    /// data source.
    /// </summary>
    /// <param name="i">Index of the benchmark tile to read.</param>
    /// <returns>The MVT byte data of the tile, or null if it is not available.</returns>
    public async Task<byte[]?> ReadVectorTile(int i)
    {
        return await _dataSource.GetTileAsync(_tiles[i]);
    }

    /// <summary>
    /// Converts the prefetched raw tile data of one benchmark tile into a
    /// <c>VectorTile</c> with the OpenMapTiles tile converter.
    /// </summary>
    /// <param name="i">Index of the prefetched raw tile data to convert.</param>
    /// <param name="j">Index of the benchmark tile that defines the target tile.</param>
    /// <returns>The converted <c>VectorTile</c>, or null if the converter is not initialized.</returns>
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

    /// <summary>Cleans up the benchmark resources.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
    }
}
