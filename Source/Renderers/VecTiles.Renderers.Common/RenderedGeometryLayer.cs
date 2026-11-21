using VecTiles.Renderers.Common.Interfaces;

namespace VecTiles.Renderers.Common;

/// <summary>
/// A rendered geometry layer (background, raster, fill, line or circle) by
/// its style layer name.
/// </summary>
/// <param name="Name">The name of the style layer.</param>
/// <param name="Renderer">Draws the layer onto the canvas.</param>
public sealed record RenderedGeometryLayer(string Name, ILayerRenderer Renderer) : IRenderedLayerEntry;
