namespace VecTiles.Renderers.Common.Exceptions;

/// <summary>
/// Exception thrown when a requested sprite cannot be found.
/// </summary>
public class SpriteNotFoundException : Exception
{
    public SpriteNotFoundException(string message) : base(message)
    {
    }
}
