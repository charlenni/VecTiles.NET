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
}
