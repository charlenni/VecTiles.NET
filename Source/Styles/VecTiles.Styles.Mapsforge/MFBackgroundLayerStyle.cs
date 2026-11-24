using VecTiles.Common.Enums;
using VecTiles.Common.Primitives;
using VecTiles.Styles.Mapsforge.Expressions;

namespace VecTiles.Styles.Mapsforge;

/// <summary>
/// Background layer of a Mapsforge render theme, created from the
/// map-background and map-background-outside attributes of the rendertheme
/// element.
/// </summary>
public class MFBackgroundLayerStyle : MFLayerStyle
{
    /// <summary>
    /// Initializes the background layer.
    /// </summary>
    /// <param name="name">Name of the layer.</param>
    /// <param name="mapBackground">Background color within the map area.</param>
    /// <param name="mapBackgroundOutside">Background color outside the map area.</param>
    public MFBackgroundLayerStyle(string name, Color mapBackground, Color mapBackgroundOutside)
        : base(name, LayerStyleType.Background, MFAllExpression.Empty, 0, 254)
    {
        MapBackground = mapBackground;
        MapBackgroundOutside = mapBackgroundOutside;
    }

    /// <summary>
    /// Gets the background color within the map area (attribute map-background).
    /// </summary>
    public Color MapBackground { get; }

    /// <summary>
    /// Gets the background color outside the map area (attribute map-background-outside).
    /// </summary>
    public Color MapBackgroundOutside { get; }
}
