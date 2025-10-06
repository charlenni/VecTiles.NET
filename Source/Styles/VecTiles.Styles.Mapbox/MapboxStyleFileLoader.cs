using Newtonsoft.Json;
using VecTiles.DataSources;

namespace VecTiles.Styles.Mapbox;

public class MapboxStyleFileLoader
{
    public static async Task<MapboxStyleFile> Load(Stream jsonStream)
    {
        MapboxStyleFile? mapboxStyleFile;

        var serializer = new JsonSerializer();

        using (var sr = new StreamReader(jsonStream))
        using (var jsonTextReader = new JsonTextReader(sr))
        {
            mapboxStyleFile = serializer.Deserialize< MapboxStyleFile>(jsonTextReader);
        }

        if (mapboxStyleFile == null)
            throw new ArgumentException("Style file isn't valid");

        CreateSources(mapboxStyleFile.Sources);

        mapboxStyleFile.Sprites = await LoadSprites(mapboxStyleFile.spriteFile);

        return mapboxStyleFile;
    }

    public static void CreateSources(Dictionary<string, MapboxSource> sources)
    {
        foreach (var source in sources)
        {
            source.Value.Name = source.Key;
            source.Value.Create();
        }
    }

    public static async Task<MapboxSpriteFile> LoadSprites(string spriteFile)
    {
        Dictionary<string, MapboxSprite>? mapboxSprites;

        var bitmapBytes = await ByteDataSource.GetBytesAsync(spriteFile + ".png");
        var jsonBytes = await ByteDataSource.GetBytesAsync(spriteFile + ".json");
        var serializer = new JsonSerializer();

        using (var stream = new MemoryStream(jsonBytes))
        using (var sr = new StreamReader(stream))
        using (var jsonTextReader = new JsonTextReader(sr))
        {
            mapboxSprites = serializer.Deserialize<Dictionary<string, MapboxSprite>>(jsonTextReader);
        }

        if (mapboxSprites == null)
        {
            throw new ArgumentException("Sprite file isn't valid");
        }

        foreach (var sprite in mapboxSprites.Values)
        {
            sprite.Binary = bitmapBytes;
        }

        return new MapboxSpriteFile(mapboxSprites);
    }
}
