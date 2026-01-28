using NetTopologySuite.Features;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;

namespace VecTiles.Renderers.Common.Interfaces;

/// <summary>
/// A RenderFactory converts different vector features of a given layer style 
/// into drawable objects.
/// </summary>
public interface IRenderFactory
{
    /// <summary>
    /// Creates a background layer for the given layer style
    /// </summary>
    /// <param name="style">The layer style to use for rendering</param>
    /// <returns>A background layer that can be drawn</returns>
    ILayerRenderer CreateBackgroundLayer(ILayerStyle style);

    /// <summary>
    /// Creates a raster layer for the given layer style and raster data
    /// </summary>
    /// <param name="style">The style to use for rendering</param>
    /// <param name="data">The byte data for the raster image to use</param>
    /// <returns>A raster layer that can be drawn</returns>
    ILayerRenderer CreateRasterLayer(ILayerStyle style, byte[] data);

    /// <summary>
    /// Creates a vector fill layer for the given layer style and features
    /// </summary>
    /// <param name="style">The layer style to use for rendering</param>
    /// <param name="features">The features to fill</param>
    /// <returns>A vector fill layer that can be drawn</returns>
    ILayerRenderer CreateVectorFillLayer(ILayerStyle style, IEnumerable<IFeature> features);

    /// <summary>
    /// Creates a vector line layer for the given layer style and features
    /// </summary>
    /// <param name="style">The layer style to use for rendering</param>
    /// <param name="features">The features to draw as lines</param>
    /// <returns>A vector line layer that can be drawn</returns>
    ILayerRenderer CreateVectorLineLayer(ILayerStyle style, IEnumerable<IFeature> features);

    /// <summary>
    /// Creates a symbol for the given tile, layer style and feature
    /// </summary>
    /// <param name="tile">The tile to which this symbol belongs</param>
    /// <param name="style">The layer style to use for rendering</param>
    /// <param name="context">The context for this symbol</param>
    /// <param name="feature">The features belonging to this symbol</param>
    /// <returns></returns>
    ISymbol? CreateSymbol(Tile tile, ILayerStyle style, EvaluationContext context, IFeature feature);
}
