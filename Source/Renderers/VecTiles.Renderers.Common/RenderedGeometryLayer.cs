using VecTiles.Renderers.Common.Interfaces;

namespace VecTiles.Renderers.Common;

/// <summary>
/// A rendered geometry layer (background, raster, fill, line or circle) by
/// its style layer name.
/// </summary>
/// <param name="Name">The name of the style layer.</param>
/// <param name="Renderer">Draws the layer onto the canvas.</param>
/// <param name="MinZoom">The style layer minzoom (-1 when not set in the style file).</param>
/// <param name="MaxZoom">The style layer maxzoom (-1 when not set in the style file).</param>
/// <param name="StyleIndex">The position of the style layer in the style file.</param>
public sealed record RenderedGeometryLayer(string Name, ILayerRenderer Renderer,
    float MinZoom = -1, float MaxZoom = -1, int StyleIndex = 0) : IRenderedLayerEntry;
