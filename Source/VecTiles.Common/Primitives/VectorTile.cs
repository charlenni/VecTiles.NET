// Copyright NetTopologySuite Contributors

namespace VecTiles.Common.Primitives;

/// <summary>
/// A vector tile.
/// </summary>
public class VectorTile
{
    /// <summary>
    /// Gets or sets the tile id.
    /// </summary>
    public ulong TileId { get; set; }

    /// <summary>
    /// Gets or sets the layers.
    /// </summary>
    public IList<Layer> Layers { get; } = new List<Layer>();

    /// <summary>
    /// Gets the is empty flag.
    /// </summary>
    public bool IsEmpty => this.Layers == null || this.Layers.All(x => x.IsEmpty);
}
