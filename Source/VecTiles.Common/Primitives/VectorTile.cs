// Copyright NetTopologySuite Contributors

using System.Runtime.InteropServices;

namespace VecTiles.Common.Primitives;

/// <summary>
/// A vector tile.
/// </summary>
public class VectorTile
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VectorTile"/> class with capacity for the given number of layers.
    /// </summary>
    /// <param name="numOfLayers">The expected number of layers. Defaults to 0.</param>
    public VectorTile(int numOfLayers = 0)
    {
        Layers = new List<Layer>(numOfLayers);
    }
    
    /// <summary>
    /// Gets or sets the tile id.
    /// </summary>
    public ulong TileId { get; set; }

    /// <summary>
    /// Gets or sets the layers.
    /// </summary>
    public IList<Layer> Layers { get; }

    /// <summary>
    /// Gets the is empty flag.
    /// </summary>
    public bool IsEmpty => Layers.Count == 0 || Layers.All(x => x.IsEmpty);
}
