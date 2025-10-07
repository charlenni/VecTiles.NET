using VecTiles.Common.Interfaces;

namespace VecTiles.Common.Primitives;

/// <summary>
/// Class holding information about bitmap regions data (sprites) in Json format
/// </summary>
public class Sprite : ISprite
{
    public Sprite(IBitmap atlas, int x, int y, int width, int height)
    {
        Atlas = atlas;
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public IBitmap Atlas { get; set; }

    public int X { get; set; }

    public int Y { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public float PixelRatio { get; set; } = 1.0f;

    public IList<float> Content { get; set; } = [];

    public IList<float> StrechX { get; set; } = [];

    public IList<float> StrechY { get; set; } = [];
}
