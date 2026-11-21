using BenchmarkDotNet.Attributes;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Converters.OpenMapTiles;
using VecTiles.DataSources.MbTiles;
using VecTiles.Renderers.Common;
using VecTiles.Renderers.Common.Interfaces;
using VecTiles.Renderers.Skia;
using VecTiles.Styles.OpenMapTiles;

namespace VecTiles.OpenMapTiles.Benchmarks;

/// <summary>
/// Benchmarks for the OpenMapTiles vector tile rendering: reads raw vector
/// tiles from an MBTiles data source at several zoom levels and renders them
/// through the style renderer into <c>IRenderedTile</c>s.
/// </summary>
[MemoryDiagnoser]
public class OMTRenderBenchmarks
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
    /// Loads the style file, creates the MBTiles data source and the style
    /// renderer and prefetches the raw tile data for all benchmark tiles.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        var styleSource = $"file://{_path}osm-liberty.json";
        var styleFile = OMTStyleFileLoader.Load(styleSource).Result;

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

    /// <summary>
    /// Reads and renders one benchmark tile: the raw tile data is fetched from
    /// the MBTiles data source, converted and rendered with the style renderer.
    /// </summary>
    /// <param name="i">Index of the benchmark tile to render.</param>
    /// <returns>The rendered tile, or null if the renderer is not initialized.</returns>
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

    /// <summary>
    /// Reads and renders one benchmark tile per layer, as a variant of
    /// <see cref="RenderVectorTile(int)"/> whose per-layer rendering path can
    /// be benchmarked separately.
    /// </summary>
    /// <param name="i">Index of the benchmark tile to render.</param>
    /// <returns>The rendered tile, or null if the renderer is not initialized.</returns>
    public async Task<IRenderedTile?> RenderVectorLayerTile(int i)
    {
        return await _renderer?.Render(_tiles[i]);
    }

    /// <summary>Cleans up the benchmark resources.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _renderer = null;
    }
}
