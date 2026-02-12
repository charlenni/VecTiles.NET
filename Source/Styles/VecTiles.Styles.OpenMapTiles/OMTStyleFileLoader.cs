using Newtonsoft.Json;
using VecTiles.Common.Primitives;
using VecTiles.DataSources;

namespace VecTiles.Styles.OpenMapTiles;

public class OMTStyleFileLoader
{
    public static async Task<OMTStyleFile> Load(Stream jsonStream)
    {
        OMTStyleFile? omtStyleFile;

        var serializer = new JsonSerializer();

        using (var sr = new StreamReader(jsonStream))
        using (var jsonTextReader = new JsonTextReader(sr))
        {
            omtStyleFile = serializer.Deserialize<OMTStyleFile>(jsonTextReader);
        }

        if (omtStyleFile == null)
            throw new ArgumentException("Style file isn't valid");

        CreateSources(omtStyleFile.Sources);

        omtStyleFile.Sprites = await LoadSprites(omtStyleFile.spriteFile);

        return omtStyleFile;
    }

    public static void CreateSources(Dictionary<string, OMTSource> sources)
    {
        foreach (var source in sources)
        {
            source.Value.Name = source.Key;
            source.Value.Create();
        }
    }

    public static async Task<SpriteDicionary> LoadSprites(string spriteFile)
    {
        Dictionary<string, Sprite>? omtSprites;

        var bitmapBytes = await ByteDataSource.GetBytesAsync(spriteFile + ".png");
        var jsonBytes = await ByteDataSource.GetBytesAsync(spriteFile + ".json");
        var serializer = new JsonSerializer();

        using (var stream = new MemoryStream(jsonBytes))
        using (var sr = new StreamReader(stream))
        using (var jsonTextReader = new JsonTextReader(sr))
        {
            omtSprites = serializer.Deserialize<Dictionary<string, Sprite>>(jsonTextReader);
        }

        if (omtSprites == null)
        {
            throw new ArgumentException("Sprite file isn't valid");
        }

        var bitmap = new Bitmap(bitmapBytes);
        
        foreach (var sprite in omtSprites.Values)
        {
            sprite.Atlas = bitmap;
        }

        return new SpriteDicionary(omtSprites);
    }
}
