using VecTiles.Renderers.Common.Interfaces;

namespace VecTiles.Renderers.Common;

/// <summary>
/// The symbols of one style layer by its style layer name. A symbol layer
/// is added even when it contains no symbols; the placement pass merges the
/// symbol layers of all tiles by name in list order.
/// </summary>
/// <param name="Name">The name of the style layer.</param>
/// <param name="Symbols">The symbol layer with the symbols of the tile.</param>
/// <param name="MinZoom">The style layer minzoom (-1 when not set in the style file).</param>
/// <param name="MaxZoom">The style layer maxzoom (-1 when not set in the style file).</param>
/// <param name="StyleIndex">The position of the style layer in the style file.</param>
public sealed record RenderedSymbolLayer(string Name, ISymbolLayer Symbols,
    float MinZoom = -1, float MaxZoom = -1, int StyleIndex = 0) : IRenderedLayerEntry;
