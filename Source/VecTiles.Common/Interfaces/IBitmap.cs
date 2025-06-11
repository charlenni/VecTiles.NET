namespace VecTiles.Common.Interfaces;

/// <summary>
/// Represents a bitmap abstraction with binary data and an optional native object.
/// </summary>
public interface IBitmap
{
    /// <summary>
    /// Gets the binary data representing the bitmap.
    /// </summary>
    byte[] Binary { get; }

    /// <summary>
    /// Gets or sets the optional native object associated with the bitmap.
    /// </summary>
    object? Native { get; set; }
}
