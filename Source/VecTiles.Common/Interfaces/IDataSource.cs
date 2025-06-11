namespace VecTiles.Common.Interfaces;

/// <summary>
/// Represents a data source that provides byte data from a specified source string.
/// Intended for one-time data retrieval operations.
/// </summary>
public interface IDataSource
{
    /// <summary>
    /// Asynchronously retrieves byte data from the specified source string.
    /// </summary>
    /// <param name="source">The source identifier (e.g., file path, URL).</param>
    /// <returns>A task representing the asynchronous operation, with the byte array result or null if not found.</returns>
    Task<byte[]?> GetBytesAsync(string source);
}
