using VecTiles.Common.Interfaces;

namespace VecTiles.Common.Primitives;

public class SpriteDicionary : Dictionary<string, ISprite>
{
    public SpriteDicionary(Dictionary<string, Sprite> sprites)
    {
        Sprites = new(sprites);
    }

    public Dictionary<string, Sprite> Sprites { get; }
}
