using VecTiles.Common.Primitives;

namespace VecTiles.Renderers.Common.Interfaces;

/// <summary>
/// Renders a single layer of a tile onto a canvas.
/// </summary>
public interface ILayerRenderer
{
    /// <summary>
    /// Draws the layer onto the given canvas.
    /// </summary>
    /// <param name="canvas">Platform specific canvas to draw on (e.g. an SKCanvas).</param>
    /// <param name="context">Context with zoom level and feature used to evaluate paint values.</param>
    void Draw(object canvas, EvaluationContext context);
}
