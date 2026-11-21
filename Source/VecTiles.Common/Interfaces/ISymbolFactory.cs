using NetTopologySuite.Features;
using VecTiles.Common.Primitives;

namespace VecTiles.Common.Interfaces;

/// <summary>
/// Factory creating the symbols (icon and text symbols) for the features of a layer.
/// </summary>
public interface ISymbolFactory
{
    /// <summary>
    /// Creates the symbol for one feature of a layer.
    /// </summary>
    /// <param name="tile">The tile the feature belongs to.</param>
    /// <param name="style">The layer style of the feature.</param>
    /// <param name="context">The evaluation context with zoom information and feature attributes.</param>
    /// <param name="feature">The feature to create the symbol for.</param>
    /// <returns>The created symbol, or <c>null</c> if no symbol is produced for this feature.</returns>
    ISymbol? CreateSymbol(Tile tile, ILayerStyle style, EvaluationContext context, IFeature feature);
}
