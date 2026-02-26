using Mapsui.Styles;

namespace VecTiles.Controls.Mapsui;

public class RenderedTileStyle() : IStyle
{
    private static long _currentStyleId; // last used style id

    public double MinVisible { get; set; } = 0;

    public double MaxVisible { get; set; } = double.MaxValue;

    public bool Enabled { get; set; } = true;

    public float Opacity { get; set; } = 1.0f;

    /// <inheritdoc />
    public long GenerationId { get; private set; }

    /// <inheritdoc />
    public virtual void Modified()
    {
        // Modified needs a new id to invalidate cached drawables.
        GenerationId = NextId();
    }

    private static long NextId()
    {
        return Interlocked.Increment(ref _currentStyleId);
    }
}
