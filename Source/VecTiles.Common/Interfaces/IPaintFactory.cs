namespace VecTiles.Common.Interfaces;

/// <summary>
/// Interface to create a style file independent class for each style
/// </summary>
public interface IPaintFactory
{
    /// <summary>
    /// Creates a paint for the given layer style.
    /// </summary>
    /// <param name="style">The layer style the paint belongs to.</param>
    /// <returns>The created paint, or <c>null</c> if the style is not supported by this factory.</returns>
    IPaint? CreatePaint(ILayerStyle style);
}
