using VecTiles.Renderers.Common.Interfaces;

namespace VecTiles.Renderers.Common;

/// <summary>
/// The symbols of one style layer by its style layer name. A symbol layer
/// is added even when it contains no symbols; the placement pass merges the
/// symbol layers of all tiles by name in list order.
/// </summary>
/// <param name="Name">The name of the style layer.</param>
/// <param name="Symbols">The symbol layer with the symbols of the tile.</param>
public sealed record RenderedSymbolLayer(string Name, ISymbolLayer Symbols) : IRenderedLayerEntry;
