using VecTiles.Common.Interfaces;

namespace VecTiles.Common.Primitives;

public class SpriteDictionary : Dictionary<string, ISprite>
{
    public SpriteDictionary(Dictionary<string, Sprite> sprites)
    {
        Sprites = new(sprites);
    }

    public Dictionary<string, Sprite> Sprites { get; }
}
