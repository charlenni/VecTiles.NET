namespace VecTiles.Styles.Mapsforge;

/// <summary>
/// A layer of the stylemenu of a Mapsforge render theme: a named, toggleable
/// combination of categories, optionally inheriting the categories of a parent
/// layer and containing individually toggleable overlay layers.
/// </summary>
public class MFMenuLayer
{
    /// <summary>
    /// Gets or sets the id of the menu layer (attribute id).
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the id of the parent menu layer its categories are
    /// inherited from (attribute parent), or null.
    /// </summary>
    public string? Parent { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the layer is shown by default
    /// in the user interface (attribute visible).
    /// </summary>
    public bool Visible { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the layer is enabled by default
    /// (attribute enabled).
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets the names of the layer per language code (name elements; the empty
    /// language code is the default name).
    /// </summary>
    public Dictionary<string, string> Names { get; } = new();

    /// <summary>
    /// Gets the categories the layer groups (cat elements).
    /// </summary>
    public List<string> Categories { get; } = [];

    /// <summary>
    /// Gets the ids of overlay menu layers of this layer (overlay elements).
    /// </summary>
    public List<string> Overlays { get; } = [];
}

/// <summary>
/// The stylemenu of a Mapsforge render theme, allowing themes to group their
/// rules in categories and to expose toggleable layers to the application.
/// </summary>
public class MFStyleMenu
{
    /// <summary>
    /// Gets or sets the id of the style menu (attribute id).
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the id of the layer enabled by default (attribute defaultvalue).
    /// </summary>
    public string DefaultValue { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the default language of the layer names (attribute defaultlang).
    /// </summary>
    public string DefaultLanguage { get; set; } = string.Empty;

    /// <summary>
    /// Gets the layers of the style menu.
    /// </summary>
    public List<MFMenuLayer> Layers { get; } = [];
}
