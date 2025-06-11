using VecTiles.Common.Enums;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;

namespace VecTiles.TileDataSources;

/// <summary>
/// This class adapts an ITileDataSource to provide vector tile data using an ITileConverter.
/// It attempts to fetch the requested tile, falling back to parent tiles if necessary,
/// and converts the tile data to a VectorTile using the provided converter.
/// </summary>
public class VectorTileDataSource : IVectorTileDataSource
{
    private readonly ITileDataSource _dataSource;
    private readonly ITileConverter _converter;

    public VectorTileDataSource(ITileDataSource dataSource, ITileConverter converter)
    {
        _dataSource = dataSource;
        _converter = converter;
    }

    /// <inheritdoc/>
    public string Name => _dataSource.Name;

    /// <inheritdoc/>
    public int MinZoom => _dataSource.MinZoom;

    /// <inheritdoc/>
    public int MaxZoom => _dataSource.MaxZoom;

    /// <inheritdoc/>
    public SourceType SourceType => _dataSource.SourceType;

    /// <inheritdoc/>
    public Task<byte[]?> GetTileAsync(Tile requestedTile) => _dataSource.GetTileAsync(requestedTile);

    /// <inheritdoc/>
    public async Task<VectorTile?> GetVectorTileAsync(Tile requestedTile)
    {
        var providedTile = requestedTile;
        var data = await _dataSource.GetTileAsync(requestedTile);

        while (data == null && providedTile.Zoom > 0)
        {
            providedTile = providedTile.Parent;
            data = await _dataSource.GetTileAsync(providedTile);
        }

        return data == null ? null : await _converter.Convert(requestedTile, providedTile, data);
    }
}
