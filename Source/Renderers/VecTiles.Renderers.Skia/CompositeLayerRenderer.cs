using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Renderers.Common.Interfaces;

namespace VecTiles.Renderers.Skia;

/// <summary>
/// Combines several layer renderers into one. Used, when the features of a
/// layer have to be drawn with more than one paint, e.g. data-driven paint
/// properties that differ from feature to feature.
/// </summary>
public class CompositeLayerRenderer : ILayerRenderer, IDisposable
{
    private readonly List<ILayerRenderer> _renderers;

    /// <summary>
    /// Create a composite layer renderer from the given layer renderers.
    /// </summary>
    /// <param name="renderers">Layer renderers drawn in the given order.</param>
    public CompositeLayerRenderer(IEnumerable<ILayerRenderer> renderers)
    {
        _renderers = [.. renderers];
    }

    /// <inheritdoc />
    public void Draw(object canvas, EvaluationContext context)
    {
        foreach (var renderer in _renderers)
        {
            renderer.Draw(canvas, context);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var renderer in _renderers)
        {
            if (renderer is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        _renderers.Clear();
    }
}
