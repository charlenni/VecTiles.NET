using Newtonsoft.Json;
using VecTiles.Common.Primitives;
using VecTiles.DataSources;

namespace VecTiles.Styles.OpenMapTiles;

public class OMTStyleFileLoader
{
    public static async Task<OMTStyleFile> Load(string source)
    {
        OMTStyleFile? omtStyleFile;

        var jsonArray = await ByteDataSource.GetBytesAsync(source).ConfigureAwait(false);
        using var jsonStream = new MemoryStream(jsonArray, writable: false);
        using var jsonReader = new JsonTextReader(new StreamReader(jsonStream));
        var serializer = new JsonSerializer();

        omtStyleFile = serializer.Deserialize<OMTStyleFile>(jsonReader);

        if (omtStyleFile == null)
            throw new ArgumentException("Style file isn't valid");

        await CreateSources(omtStyleFile.Sources).ConfigureAwait(false);

        omtStyleFile.Sprites = await LoadSprites(omtStyleFile.spriteFile).ConfigureAwait(false);

        return omtStyleFile;
    }

    public static async Task CreateSources(Dictionary<string, OMTSource> sources)
    {
        foreach (var source in sources)
        {
            source.Value.Name = source.Key;
            await source.Value.CreateAsync().ConfigureAwait(false);
        }
    }

    public static async Task<SpriteDictionary> LoadSprites(string spriteFile)
    {
        Dictionary<string, Sprite>? omtSprites;

        byte[] bitmapBytes;
        byte[] jsonBytes;

        try
        {
            bitmapBytes = await ByteDataSource.GetBytesAsync(spriteFile + "@2x.png").ConfigureAwait(false);
            jsonBytes = await ByteDataSource.GetBytesAsync(spriteFile + "@2x.json").ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Fall back to the unsuffixed versions. Both are reloaded so that
            // json and png always match each other.
            bitmapBytes = await ByteDataSource.GetBytesAsync(spriteFile + ".png").ConfigureAwait(false);
            jsonBytes = await ByteDataSource.GetBytesAsync(spriteFile + ".json").ConfigureAwait(false);
        }

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

        return new SpriteDictionary(omtSprites);
    }
}
