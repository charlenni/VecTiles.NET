namespace VecTiles.Common.Interfaces;

/// <summary>
/// Represents a rectangular region within a bitmap.
/// </summary>
public interface IBitmapRegion
{
    /// <summary>
    /// Gets the X-coordinate of the region's top-left corner.
    /// </summary>
    int X { get; }

    /// <summary>
    /// Gets the Y-coordinate of the region's top-left corner.
    /// </summary>
    int Y { get; }

    /// <summary>
    /// Gets the width of the region.
    /// </summary>
    int Width { get; }

    /// <summary>
    /// Gets the height of the region.
    /// </summary>
    int Height { get; }
}
