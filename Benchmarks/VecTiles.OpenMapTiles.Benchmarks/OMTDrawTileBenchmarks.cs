using BenchmarkDotNet.Attributes;
using BruTile;
using Mapsui.Providers.Wfs.Utilities;
using SkiaSharp;
using VecTiles.Controls.Mapsui;
using MapRenderer = Mapsui.Rendering.Skia.MapRenderer;
using Viewport = Mapsui.Viewport;

namespace VecTiles.OpenMapTiles.Benchmarks;

/// <summary>
/// Measures the drawing speed of the tile rendering used by
/// <c>VecTiles.Renderers.Skia.Test.RenderedTileTests</c>: the center tile
/// (Z19, X274582, Y183615) is drawn on a canvas, its surrounding tiles (3x3)
/// and finally the full layer path with the placed symbols. The vector tiles
/// are rendered once in the setup and the placement pass is run once
/// synchronously, so the benchmarks measure only the drawing.
/// </summary>
[MemoryDiagnoser]
public class OMTDrawTileBenchmarks
{
    private const string _path = "Files";
    private const int Zoom = 19;
    private const int CenterX = 274582;
    private const int CenterY = 183615;
    private const int TileSize = 512;
    private const double WorldWidth = 40075016.686;
    private const double WorldHalf = WorldWidth / 2;

    private OMTRenderedTileSource _tileSource = null!;
    private RenderedTileLayer _tileLayer = null!;
    private Mapsui.Rendering.RenderService _renderService = null!;
    private Viewport _viewport = new();
    private TileInfo[] _tileInfos = null!;

    static OMTDrawTileBenchmarks()
    {
        MapRenderer.RegisterLayerRenderer("rendered-tile-layer", RenderedTileLayerRenderer.Draw);
    }

    [GlobalSetup]
    public async Task Setup()
    {
        var styleSource = $"file://{_path}osm-liberty.json";
        _tileSource = new OMTRenderedTileSource(styleSource);
        _tileLayer = new RenderedTileLayer(_tileSource)
        {
            SynchronousPlacement = true
        };
        _renderService = new Mapsui.Rendering.RenderService();

        const int size = 3 * TileSize;
        double resolution = 78271.51696402031 / Math.Pow(2, Zoom); // initial resolution of a 512 tile in EPSG:3857
        double tileSpan = TileSize * resolution;

        double centerXWorld = -WorldHalf + (CenterX + 0.5) * tileSpan;
        double centerYWorld = WorldHalf - (CenterY + 0.5) * tileSpan;
        _viewport = new Viewport(centerXWorld, centerYWorld, resolution, 0, size, size);

        _tileInfos = new TileInfo[9];
        int index = 0;
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                _tileInfos[index++] = CreateTileInfo(CenterX + dx, CenterY + dy);
            }
        }

        // Render all tiles once and run the synchronous placement pass once,
        // so the benchmarks only measure the drawing
        foreach (var tileInfo in _tileInfos)
        {
            await _tileSource.GetTileAsync(tileInfo);
        }

        using var warmup = new SKBitmap(3 * TileSize, 3 * TileSize);
        using var warmupCanvas = new SKCanvas(warmup);
        RenderedTileLayerRenderer.Draw(warmupCanvas, _viewport, _tileLayer, _renderService);
    }

    /// <summary>Draws one single rendered tile, without neighbors and symbols.</summary>
    [Benchmark]
    public void DrawCenterTile()
    {
        using var bitmap = new SKBitmap(TileSize, TileSize);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Beige);

        var tile = _tileSource.GetTileAsync(_tileInfos[4]).Result;

        if (tile != null)
        {
            RenderedTileLayerRenderer.DrawGeometryTile(canvas, _viewport, _tileInfos[4], tile);
        }
    }

    /// <summary>Draws the 3x3 surrounding tiles, as in <c>RenderedTileTests</c>.</summary>
    [Benchmark]
    public void DrawSurroundingTiles3x3()
    {
        using var bitmap = new SKBitmap(3 * TileSize, 3 * TileSize);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Beige);

        foreach (var tileInfo in _tileInfos)
        {
            var tile = _tileSource.GetTileAsync(tileInfo).Result;

            if (tile != null)
            {
                RenderedTileLayerRenderer.DrawGeometryTile(canvas, _viewport, tileInfo, tile);
            }
        }
    }

    /// <summary>Draws the full path of <c>RenderedTileTests</c>: 3x3 tiles plus the placed symbols of the placement run.</summary>
    [Benchmark]
    public void DrawTileWithSymbols3x3()
    {
        using var bitmap = new SKBitmap(3 * TileSize, 3 * TileSize);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Beige);

        RenderedTileLayerRenderer.Draw(canvas, _viewport, _tileLayer, _renderService);
    }

    private static TileInfo CreateTileInfo(int tileX, int tileY)
    {
        double resolution = 78271.51696402031 / Math.Pow(2, Zoom);
        double tileSpan = TileSize * resolution;

        double west = -WorldHalf + tileX * tileSpan;
        double north = WorldHalf - tileY * tileSpan;

        return new TileInfo
        {
            Index = new TileIndex(tileX, tileY, Zoom),
            Extent = new Extent(west, north - tileSpan, west + tileSpan, north)
        };
    }
}
