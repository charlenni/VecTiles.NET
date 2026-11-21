namespace VecTiles.Renderers.Common.Interfaces;

/// <summary>
/// One entry in the ordered layer list of an <see cref="IRenderedTile"/>.
/// The list is in style file order (bottom layer first), so consuming code
/// neither needs nor must rely on dictionary insertion order.
/// </summary>
public interface IRenderedLayerEntry
{
    /// <summary>
    /// Gets the name of the style layer.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the position of the style layer in the style file (the index in
    /// the unfiltered style list). Not every tile carries an entry for every
    /// style layer (missing data), so the merged order of the tiles must be
    /// built from this index, not from the first occurrence across the tiles.
    /// </summary>
    public int StyleIndex { get; }
}
