using VecTiles.Common.Interfaces;
using System.Collections.Concurrent;
using System.Globalization;
using System.Xml.Linq;
using VecTiles.Common.Primitives;
using VecTiles.DataSources;

namespace VecTiles.Styles.Mapsforge;

/// <summary>
/// Resolves the icon and pattern sources of a Mapsforge render theme (src
/// attributes like "jar:symbols/amenity/library.svg") into sprites. Sources
/// are resolved relative to the base location of the theme; SVG sources
/// become vector sprites (rendered as vectors by the renderer), raster sources
/// (PNG) become atlas sprites of a single image.
/// </summary>
public class MFSpriteFactory
{
    /// <summary>
    /// Default size of symbols without intrinsic size and without size
    /// attributes (mapsforge renders such SVGs at 20x20 px).
    /// </summary>
    private const float DefaultSymbolSize = 20;

    private readonly string? _baseDirectory;

    // Sprites are shared between all layers referencing the same source with
    // the same size attributes, and are loaded concurrently (parallel tile
    // rendering), so access must be thread-safe
    private readonly ConcurrentDictionary<string, ISprite?> _sprites = new();

    /// <summary>
    /// Initializes the sprite factory with the base location icon and pattern
    /// sources are resolved against (URL notation of the ByteDataSource, e.g.
    /// "file:///themes/mapsforge"; sources of the theme are relative to it).
    /// Null resolves relative sources against the working directory.
    /// </summary>
    /// <param name="baseDirectory">Base location of the theme, or null.</param>
    public MFSpriteFactory(string? baseDirectory)
    {
        _baseDirectory = baseDirectory;
    }

    /// <summary>
    /// Gets the sprite for a src attribute of the theme with the requested
    /// size attributes, loading and caching it on first need.
    /// </summary>
    /// <param name="src">Source of the sprite (the src attribute of the theme).</param>
    /// <param name="symbolWidth">Requested width in px (attribute symbol-width), or null.</param>
    /// <param name="symbolHeight">Requested height in px (attribute symbol-height), or null.</param>
    /// <param name="symbolPercent">Requested size in percent of the intrinsic size (attribute symbol-percent), or null.</param>
    /// <returns>The sprite, or null when the source cannot be loaded.</returns>
    public ISprite? GetSprite(string? src, int? symbolWidth = null, int? symbolHeight = null, int? symbolPercent = null)
    {
        if (string.IsNullOrEmpty(src))
        {
            return null;
        }

        var cacheKey = $"{src}?w={symbolWidth}&h={symbolHeight}&p={symbolPercent}";

        return _sprites.GetOrAdd(cacheKey, _ => CreateSprite(src, symbolWidth, symbolHeight, symbolPercent));
    }

    /// <summary>
    /// Loads the data of a sprite source and creates the matching sprite.
    /// </summary>
    private ISprite? CreateSprite(string src, int? symbolWidth, int? symbolHeight, int? symbolPercent)
    {
        byte[] data;

        foreach (var candidate in ResolveSourceCandidates(src))
        {
            try
            {
                data = ByteDataSource.GetBytesAsync(candidate).ConfigureAwait(false).GetAwaiter().GetResult();
            }
            catch (Exception)
            {
                // A missing icon or pattern must not prevent the theme from
                // rendering; the next candidate is tried
                continue;
            }

            return CreateSpriteOfData(src, data, symbolWidth, symbolHeight, symbolPercent);
        }

        // No candidate of the source could be loaded
        return null;
    }

    /// <summary>
    /// Creates the sprite of already loaded source data.
    /// </summary>
    private ISprite CreateSpriteOfData(string src, byte[] data, int? symbolWidth, int? symbolHeight, int? symbolPercent)
    {
        if (IsSvg(src, data))
        {
            var (intrinsicWidth, intrinsicHeight) = ParseSvgSize(data);
            var (width, height) = GetDisplaySize(intrinsicWidth, intrinsicHeight, symbolWidth, symbolHeight, symbolPercent);

            return new VectorSprite(data, width, height, intrinsicWidth, intrinsicHeight);
        }

        // Raster sprite: one image of its own, sized by the request or by the
        // (PNG) intrinsic size
        var (rasterWidth, rasterHeight) = ParsePngSize(data) ?? GetDisplaySize(
            DefaultSymbolSize, DefaultSymbolSize, symbolWidth, symbolHeight, symbolPercent);
        var (rasterDisplayWidth, rasterDisplayHeight) = GetDisplaySize(
            rasterWidth, rasterHeight, symbolWidth, symbolHeight, symbolPercent);

        return new Sprite(new Bitmap(data), 0, 0, (int)MathF.Ceiling(rasterDisplayWidth), (int)MathF.Ceiling(rasterDisplayHeight));
    }

    /// <summary>
    /// Gets the candidate sources of a src attribute, in the order they are
    /// tried: the primary resolution (<see cref="ResolveSource"/>) plus the
    /// fallback resolutions. Mapsforge distributions often keep the jar:/
    /// assets: resources of several themes (the symbols/ and patterns/
    /// folders) one level above the theme file, shared between them, so this
    /// location is tried as well when the primary one does not exist.
    /// </summary>
    private IEnumerable<string> ResolveSourceCandidates(string src)
    {
        yield return ResolveSource(src);

        if (_baseDirectory == null || src.Contains("://", StringComparison.OrdinalIgnoreCase))
        {
            yield break;
        }

        var parentIndex = _baseDirectory.LastIndexOf('/');
        var schemeLength = ByteDataSource.FileScheme.Length + 3; // "file:///"
        if (parentIndex < schemeLength + 1)
        {
            yield break;
        }

        var stripped = src;
        if (stripped.StartsWith("jar:", StringComparison.OrdinalIgnoreCase))
        {
            stripped = stripped[4..];
        }
        else if (stripped.StartsWith("assets:", StringComparison.OrdinalIgnoreCase))
        {
            stripped = stripped[7..];
        }

        if (stripped.Contains("://", StringComparison.OrdinalIgnoreCase) ||
            stripped.StartsWith('/'))
        {
            yield break;
        }

        yield return ToByteDataSourceUrl($"{_baseDirectory[..parentIndex]}/{stripped}");
    }

    /// <summary>
    /// Converts a plain file system path to the file URL notation of the
    /// byte data source ("file://" on Unix-like systems, "file:///" on
    /// Windows); paths that already carry a scheme are kept as-is.
    /// </summary>
    private static string ToByteDataSourceUrl(string path)
    {
        if (path.Contains("://"))
        {
            return path;
        }

        return path.StartsWith('/') ? $"file://{path}" : $"file:///{path}";
    }

    /// <summary>
    /// Resolves a src attribute of the theme to the source notation of the
    /// ByteDataSource: the schemes "jar:" and "assets:" (used by mapsforge
    /// themes for theme relative resources) are stripped, "file:" sources are
    /// kept, relative sources are resolved against the base directory.
    /// </summary>
    private string ResolveSource(string src)
    {
        // Strip the mapsforge resource schemes
        if (src.StartsWith("jar:", StringComparison.OrdinalIgnoreCase))
        {
            src = src[4..];
        }
        else if (src.StartsWith("assets:", StringComparison.OrdinalIgnoreCase))
        {
            src = src[7..];
        }

        // Absolute sources with a scheme (like "file://") or an URL are used as-is
        if (src.Contains("://"))
        {
            return src;
        }

        var path = src.StartsWith('/') || Path.IsPathRooted(src)
            ? src
            : _baseDirectory != null
                ? $"{_baseDirectory.TrimEnd('/')}/{src}"
                : Path.GetFullPath(src);

        // The composed path may already carry a scheme (the base directory is
        // given in ByteDataSource URL notation)
        if (path.Contains("://"))
        {
            return path;
        }

        // Plain paths are loaded through the file scheme of the ByteDataSource
        return ToByteDataSourceUrl(path);
    }

    /// <summary>
    /// Checks whether the sprite data of a source is vector data (SVG) from the
    /// file extension of the source or from a sniff of the first bytes of the
    /// data (also detects an "&lt;svg" element after an XML declaration).
    /// </summary>
    private static bool IsSvg(string src, byte[] data)
    {
        if (src.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (src.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Sniff the first bytes of the content for an opening svg element
        var window = Math.Min(data.Length, 512);
        const string marker = "<svg";

        for (var index = 0; index + marker.Length <= window; index++)
        {
            if (ToLower(data[index]) == marker[0] &&
                ToLower(data[index + 1]) == marker[1] &&
                ToLower(data[index + 2]) == marker[2] &&
                ToLower(data[index + 3]) == marker[3])
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Lowercases an ASCII byte, other bytes are returned unchanged.
    /// </summary>
    private static char ToLower(byte value)
    {
        return value is >= (byte)'A' and <= (byte)'Z' ? (char)(value + 32) : (char)value;
    }

    /// <summary>
    /// Gets the display size of a symbol from its intrinsic size and the
    /// requested size attributes (symbol-width, symbol-height, symbol-percent):
    /// a missing dimension follows the aspect ratio of the intrinsic size, the
    /// percent attribute scales the size.
    /// </summary>
    private static (float Width, float Height) GetDisplaySize(
        float intrinsicWidth, float intrinsicHeight, int? symbolWidth, int? symbolHeight, int? symbolPercent)
    {
        var width = symbolWidth ?? (symbolHeight.HasValue
            ? intrinsicWidth * symbolHeight.Value / intrinsicHeight
            : intrinsicWidth);
        var height = symbolHeight ?? (symbolWidth.HasValue
            ? intrinsicHeight * symbolWidth.Value / intrinsicWidth
            : intrinsicHeight);

        if (symbolPercent.HasValue)
        {
            width *= symbolPercent.Value / 100f;
            height *= symbolPercent.Value / 100f;
        }

        return (width, height);
    }

    /// <summary>
    /// Parses the intrinsic size of an SVG document from its width/height
    /// attributes and, when those are missing or relative, from its viewBox.
    /// </summary>
    private static (float Width, float Height) ParseSvgSize(byte[] data)
    {
        try
        {
            using var stream = new MemoryStream(data, writable: false);
            var document = XDocument.Load(stream);
            var svg = document.Root;

            if (svg == null)
            {
                return (DefaultSymbolSize, DefaultSymbolSize);
            }

            float? viewBoxWidth = null;
            float? viewBoxHeight = null;

            if (svg.Attribute("viewBox")?.Value is string viewBox)
            {
                var parts = viewBox.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length == 4 &&
                    float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var vbw) &&
                    float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var vbh))
                {
                    viewBoxWidth = vbw;
                    viewBoxHeight = vbh;
                }
            }

            var width = ParseSvgLength(svg.Attribute("width")?.Value) ?? viewBoxWidth;
            var height = ParseSvgLength(svg.Attribute("height")?.Value) ?? viewBoxHeight;

            return (width ?? viewBoxWidth ?? DefaultSymbolSize, height ?? viewBoxHeight ?? DefaultSymbolSize);
        }
        catch (Exception)
        {
            // A malformed SVG falls back to the default size
            return (DefaultSymbolSize, DefaultSymbolSize);
        }
    }

    /// <summary>
    /// Parses an SVG length attribute value in px units (values with "%", "pt"
    /// or other units are ignored).
    /// </summary>
    private static float? ParseSvgLength(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        value = value.Trim();
        if (value.EndsWith("px", StringComparison.OrdinalIgnoreCase))
        {
            value = value[..^2].Trim();
        }

        if (value.Contains('%') || value.Contains(',') || value.Contains(' '))
        {
            return null;
        }

        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) && result > 0
            ? result
            : null;
    }

    /// <summary>
    /// Parses the pixel size of a PNG file from its IHDR chunk.
    /// </summary>
    private static (float Width, float Height)? ParsePngSize(byte[] data)
    {
        // PNG signature (8 bytes) + IHDR chunk header (8 bytes) + width/height
        if (data.Length < 24 ||
            data[0] != 0x89 || data[1] != (byte)'P' || data[2] != (byte)'N' || data[3] != (byte)'G')
        {
            return null;
        }

        var width = (data[16] << 24) | (data[17] << 16) | (data[18] << 8) | data[19];
        var height = (data[20] << 24) | (data[21] << 16) | (data[22] << 8) | data[23];

        return (width, height);
    }
}
