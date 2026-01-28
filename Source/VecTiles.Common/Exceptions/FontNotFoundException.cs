namespace VecTiles.Common.Exceptions;

/// <summary>
/// Exception thrown when a specified font cannot be found. 
/// </summary>
public class FontNotFoundException(string message) : Exception(message);
