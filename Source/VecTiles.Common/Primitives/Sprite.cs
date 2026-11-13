using VecTiles.Common.Interfaces;

namespace VecTiles.Common.Primitives;

/// <summary>
/// Class holding information about bitmap regions data (sprites) in Json format
/// </summary>
public class Sprite : ISprite
{
    public Sprite(IBitmap atlas, int x, int y, int width, int height, float pixelRatio = 1.0f)
    {
        Atlas = atlas;
        X = x;
        Y = y;
        Width = width;
        Height = height;
        PixelRatio = pixelRatio;
    }

    public IBitmap Atlas { get; set; }

    public int X { get; set; }

    public int Y { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public object? Native { get; set; }

    public float PixelRatio { get; set; }

    public IList<float> Content { get; set; } = [];

    public IList<float> StrechX { get; set; } = [];

    public IList<float> StrechY { get; set; } = [];
}
