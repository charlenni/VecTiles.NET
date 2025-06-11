using VecTiles.Common.Interfaces;

namespace VecTiles.Common.Primitives;

/// <summary>
/// Represents a bitmap with binary data and an optional native object.
/// Implements the <see cref="IBitmap"/> interface.
/// </summary>
public class Bitmap : IBitmap
{
    public Bitmap(byte[] binary)
    {
        Binary = binary;
    }

    /// <inheritdoc/>
    public object? Native { get; set; }

    /// <inheritdoc/>
    public byte[] Binary { get; }
}
