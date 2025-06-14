using VecTiles.Common.Enums;
using VecTiles.Common.Primitives;

namespace VecTiles.Common.Interfaces;

/// <summary>
/// ITileStyle defines the contract for a style layer in the vector tile rendering system.
/// It includes properties for style identification, type, zoom range, visibility, filtering, and source information.
/// The Update method allows the style to refresh its paint properties based on the provided evaluation context.
/// </summary>
public interface ILayerStyle
{
    /// <summary>
    /// Name of style
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Type of this style
    /// </summary>
    LayerStyleType StyleType { get; }

    /// <summary>
    /// Minimal zoom from which this style layer is used
    /// </summary>
    int MinZoom { get; }

    /// <summary>
    /// Maximal zoom up to which this style layer is used
    /// </summary>
    int MaxZoom { get; }

    /// <summary>
    /// Is style layer visible
    /// </summary>
    bool Visible { get; }

    /// <summary>
    /// Filter to get all features for which this style is valid
    /// </summary>
    IFilter Filter { get; }

    /// <summary>
    /// Name of source this style belongs to 
    /// </summary>
    string Source { get; }

    /// <summary>
    /// Name of source layer this style belongs to 
    /// </summary>
    string SourceLayer { get; }

    /// <summary>
    /// Update the paints in the style with the new values in context
    /// </summary>
    /// <param name="context"></param>
    void Update(EvaluationContext context);
}
