using System.Text.Json;
using VecTiles.Common.Interfaces;
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
    /// <param name="createSources">Whether to create the tile data sources of the style's
    /// sources; false leaves <see cref="OMTSource.DataSource"/> null, so the caller can attach
    /// own data sources (e.g. a Mapsforge map file given independently of the style file).</param>
    /// <param name="vectorTileConnectionFactory">Factory for the tile converter of vector
    /// sources (see <see cref="OMTSource.VectorTileConverterFactory"/>); vector sources that
    /// load through this loader need it to provide renderable tiles.</param>
    /// <returns>The loaded style file with sources and sprites initialized.</returns>
    /// <exception cref="ArgumentException">Thrown if the file isn't a valid style file.</exception>
    public static async Task<OMTStyleFile> Load(string source, bool createSources = true,
        Func<ITileDataSource, ITileConverter>? vectorTileConnectionFactory = null)
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

        if (vectorTileConnectionFactory != null)
        {
            foreach (var omtSource in omtStyleFile.Sources.Values)
                omtSource.VectorTileConverterFactory = vectorTileConnectionFactory;
        }

        if (createSources)
        {
            await CreateSources(omtStyleFile.Sources).ConfigureAwait(false);

            ApplySourceCenterAndZoom(omtStyleFile);
        }

        omtStyleFile.Sprites = await LoadSprites(omtStyleFile.SpriteDefinitions).ConfigureAwait(false);

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
        return await LoadSprites(new OMTSpriteDefinitionCollection
        {
            new OMTSpriteDefinition { Url = spriteFile }
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Loads the sprite atlases of the given sprite definitions (a single
    /// sprite URL, or the id/URL pairs of the MapLibre style spec) and merges
    /// them into one dictionary. Sprites of a definition with an id are also
    /// registered under "id:name", so style properties can reference them
    /// prefixed.
    /// </summary>
    /// <param name="spriteDefinitions">The sprite definitions of the style file.</param>
    /// <returns>The sprite dictionary with each sprite's atlas set to its loaded bitmap.</returns>
    /// <exception cref="ArgumentException">Thrown if a sprite JSON isn't valid.</exception>
    public static async Task<SpriteDictionary> LoadSprites(OMTSpriteDefinitionCollection spriteDefinitions)
    {
        var sprites = new Dictionary<string, Sprite>();

        foreach (var definition in spriteDefinitions)
        {
            var (bitmap, atlasSprites) = await LoadSpriteAtlas(definition.Url).ConfigureAwait(false);

            foreach (var (name, sprite) in atlasSprites)
            {
                sprite.Atlas = bitmap;

                // Sprites are always resolvable by their plain name; when
                // several definitions contain the same name, the first wins
                if (!sprites.ContainsKey(name))
                    sprites.Add(name, sprite);

                if (!string.IsNullOrEmpty(definition.Id))
                    sprites.TryAdd($"{definition.Id}:{name}", sprite);
            }
        }

        return new SpriteDictionary(sprites);
    }

    /// <summary>
    /// Loads a sprite atlas from its base path ("@2x" version when available,
    /// unsuffixed version as fallback).
    /// </summary>
    /// <param name="spriteFile">Base path or URL of the sprite file, without file extension.</param>
    /// <returns>The atlas bitmap and the dictionary of the atlas' sprite descriptions.</returns>
    /// <exception cref="ArgumentException">Thrown if the sprite JSON isn't valid.</exception>
    private static async Task<(Bitmap Bitmap, Dictionary<string, Sprite> Sprites)> LoadSpriteAtlas(string spriteFile)
    {
        Dictionary<string, Sprite>? atlasSprites;

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
            atlasSprites = JsonSerializer.Deserialize<Dictionary<string, Sprite>>(stream, StyleJson.Options);
        }

        if (atlasSprites == null)
        {
            throw new ArgumentException("Sprite file isn't valid");
        }

        return (new Bitmap(bitmapBytes), atlasSprites);
    }
}
