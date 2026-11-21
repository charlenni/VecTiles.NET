using VecTiles.Common.Interfaces;

namespace VecTiles.Common.Primitives;

/// <summary>
/// Represents a bitmap with binary data and an optional native object.
/// Implements the <see cref="IBitmap"/> interface.
/// </summary>
public class Bitmap : IBitmap
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Bitmap"/> class with the given binary data.
    /// </summary>
    /// <param name="binary">The binary data of the bitmap.</param>
    public Bitmap(byte[] binary)
    {
        Binary = binary;
    }

    /// <inheritdoc/>
    public object? Native { get; set; }

    /// <inheritdoc/>
    public byte[] Binary { get; }
}
