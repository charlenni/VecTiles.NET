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

        // Resolve relative source file URLs against the folder of the style
        // file, so styles are independent of the working directory.
        var styleDirectory = GetStyleFileDirectory(source);

        if (styleDirectory != null)
        {
            foreach (var omtSource in omtStyleFile.Sources.Values)
                omtSource.ResolveRelativeUrls(styleDirectory);
        }

        await CreateSources(omtStyleFile.Sources).ConfigureAwait(false);

        ApplySourceCenterAndZoom(omtStyleFile);

        omtStyleFile.Sprites = await LoadSprites(omtStyleFile.spriteFile).ConfigureAwait(false);

        return omtStyleFile;
    }

    /// <summary>
    /// Directory part of a style file source, or null when the source isn't a
    /// file system path (e.g. a network URL) and relative paths cannot be
    /// resolved against it.
    /// </summary>
    /// <param name="source">File path or URL of the style file.</param>
    /// <returns>The directory containing the style file, without trailing slash.</returns>
    private static string? GetStyleFileDirectory(string source)
    {
        if (!source.StartsWith(ByteDataSource.FileScheme + "://", StringComparison.OrdinalIgnoreCase))
            return null;

        var path = source.Substring(ByteDataSource.FileScheme.Length + 3);
        var lastSeparator = path.LastIndexOf('/');

        return lastSeparator < 0 ? string.Empty : path.Substring(0, lastSeparator);
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
    /// Fills Center and Zoom of the style file from the first data source that
    /// provides them (e.g. the metadata of an MBTiles file), for entries the
    /// style file doesn't set itself.
    /// </summary>
    /// <param name="styleFile">The loaded style file.</param>
    private static void ApplySourceCenterAndZoom(OMTStyleFile styleFile)
    {
        if (styleFile.Center.Length >= 2 && styleFile.Zoom.HasValue)
            return;

        var source = styleFile.Sources.Values.FirstOrDefault(s => s.Center.Length >= 2);
        if (source == null)
            return;

        if (styleFile.Center.Length < 2)
            styleFile.Center = source.Center;

        if (!styleFile.Zoom.HasValue)
            styleFile.Zoom = source.Zoom;
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
