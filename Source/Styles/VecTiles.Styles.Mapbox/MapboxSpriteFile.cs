using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;

namespace VecTiles.Styles.Mapbox;

public record MapboxSpriteFile
{
    public MapboxSpriteFile(byte[] bitmap, Dictionary<string, MapboxSprite> sprites)
    {
        Bitmap = new Bitmap(bitmap);
        Sprites = sprites;
    }

    public IBitmap Bitmap { get; }

    public Dictionary<string, MapboxSprite> Sprites { get; } = new();
}
