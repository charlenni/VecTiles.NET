namespace VecTiles.Styles.Mapbox;

public record MapboxSpriteFile
{
    public MapboxSpriteFile(Dictionary<string, MapboxSprite> sprites)
    {
        Sprites = sprites;
    }

    public Dictionary<string, MapboxSprite> Sprites { get; } = new();
}
