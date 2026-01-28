namespace VecTiles.Common.Exceptions;

/// <summary>
/// Exception thrown when a requested sprite cannot be found.
/// </summary>
public class SpriteNotFoundException(string message) : Exception(message);
