using VecTiles.Common.Interfaces;

namespace VecTiles.Common.Primitives;

/// <summary>
/// A dictionary mapping sprite names to <see cref="Sprite"/> instances, viewable as sprites of type <see cref="ISprite"/>.
/// </summary>
/// <param name="sprites">The initial sprite entries by name.</param>
public class SpriteDictionary(Dictionary<string, Sprite> sprites) : Dictionary<string, ISprite>
{
    /// <summary>
    /// Gets the sprites by name.
    /// </summary>
    public Dictionary<string, Sprite> Sprites { get; } = new(sprites);
}
