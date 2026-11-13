using VecTiles.Common.Interfaces;

namespace VecTiles.Common.Primitives;

public class SpriteDictionary(Dictionary<string, Sprite> sprites) : Dictionary<string, ISprite>
{
    public Dictionary<string, Sprite> Sprites { get; } = new(sprites);
}
