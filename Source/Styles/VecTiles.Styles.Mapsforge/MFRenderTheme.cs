using VecTiles.Common.Primitives;

namespace VecTiles.Styles.Mapsforge;

/// <summary>
/// A Mapsforge render theme (XML render theme file, see
/// https://github.com/mapsforge/mapsforge/blob/master/docs/Rendertheme.md):
/// the global settings of the theme, its stylemenu and the flattened list of
/// style layers created from its rules.
/// </summary>
public class MFRenderTheme
{
    /// <summary>
    /// Gets or sets the name of the render theme (its file name without extension).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the version of the render theme file format (attribute version).
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// Gets or sets the background color within the map area
    /// (attribute map-background).
    /// </summary>
    public Color MapBackground { get; set; } = Colors.White;

    /// <summary>
    /// Gets or sets the background color outside the map area
    /// (attribute map-background-outside).
    /// </summary>
    public Color MapBackgroundOutside { get; set; } = Colors.White;

    /// <summary>
    /// Gets or sets the factor stroke widths are scaled with
    /// (attribute base-stroke-width).
    /// </summary>
    public float BaseStrokeWidth { get; set; } = 1;

    /// <summary>
    /// Gets or sets the factor text sizes are scaled with
    /// (attribute base-text-size).
    /// </summary>
    public float BaseTextSize { get; set; } = 1;

    /// <summary>
    /// Gets or sets the stylemenu of the render theme, or null when it has none.
    /// </summary>
    public MFStyleMenu? StyleMenu { get; set; }

    /// <summary>
    /// Gets the flattened style layers of the render theme: one layer per
    /// drawing instruction (line, area, circle, symbol, caption, lineSymbol,
    /// pathText), in document order of the render theme.
    /// </summary>
    public List<MFLayerStyle> Layers { get; } = [];

    /// <summary>
    /// Gets or sets the name of the data source the layers of the theme
    /// render from. Mapsforge render themes render all features of the map
    /// file, independent of source layers, so the layers reference the single
    /// source layer "mapsforge" of the raw Mapsforge data.
    /// </summary>
    public string Source { get; set; } = "mapsforge";
}
