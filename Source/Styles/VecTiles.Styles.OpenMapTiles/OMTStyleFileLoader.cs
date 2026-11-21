using System.Text.Json;
using VecTiles.Common.Primitives;
using VecTiles.DataSources;
using VecTiles.Styles.OpenMapTiles.Json;

namespace VecTiles.Styles.OpenMapTiles;

/// <summary>
/// Loads an OpenMapTiles style file from a file path or URL, including its
/// sources and sprites.
/// </summary>
public class OMTStyleFileLoader
{
    /// <summary>
    /// Loads and deserializes the style file, creates its data sources, and
    /// loads the referenced sprite file.
    /// </summary>
    /// <param name="source">File path or URL of the style file.</param>
    /// <returns>The loaded style file with sources and sprites initialized.</returns>
    /// <exception cref="ArgumentException">Thrown if the file isn't a valid style file.</exception>
    public static async Task<OMTStyleFile> Load(string source)
    {
        OMTStyleFile? omtStyleFile;

        var jsonArray = await ByteDataSource.GetBytesAsync(source).ConfigureAwait(false);
        using var jsonStream = new MemoryStream(jsonArray, writable: false);

        omtStyleFile = JsonSerializer.Deserialize<OMTStyleFile>(jsonStream, StyleJson.Options);

        if (omtStyleFile == null)
            throw new ArgumentException("Style file isn't valid");

        await CreateSources(omtStyleFile.Sources).ConfigureAwait(false);

        omtStyleFile.Sprites = await LoadSprites(omtStyleFile.spriteFile).ConfigureAwait(false);

        return omtStyleFile;
    }

    /// <summary>
    /// Names each source by its key in the dictionary and creates its data source.
    /// </summary>
    /// <param name="sources">The source descriptions of the style file, keyed by name.</param>
    /// <returns>A task that completes when all sources have been created.</returns>
    public static async Task CreateSources(Dictionary<string, OMTSource> sources)
    {
        foreach (var source in sources)
        {
            source.Value.Name = source.Key;
            await source.Value.CreateAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Loads a sprite atlas from its base path ("@2x" version when available,
    /// unsuffixed version as fallback), returning the dictionary of sprites.
    /// </summary>
    /// <param name="spriteFile">Base path or URL of the sprite file, without file extension.</param>
    /// <returns>The sprite dictionary with each sprite's atlas set to the loaded bitmap.</returns>
    /// <exception cref="ArgumentException">Thrown if the sprite JSON isn't valid.</exception>
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

        using (var stream = new MemoryStream(jsonBytes, writable: false))
        {
            omtSprites = JsonSerializer.Deserialize<Dictionary<string, Sprite>>(stream, StyleJson.Options);
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
