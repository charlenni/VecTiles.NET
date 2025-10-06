namespace VecTiles.Renderers.Common.Exceptions;

/// <summary>
/// Exception thrown when a specified font cannot be found. 
/// </summary>
public class FontNotFoundException : Exception
{
    public FontNotFoundException(string message) : base(message)
    {
    }
}
