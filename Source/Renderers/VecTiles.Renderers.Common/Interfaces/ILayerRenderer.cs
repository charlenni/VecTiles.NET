using VecTiles.Common.Primitives;

namespace VecTiles.Renderers.Common.Interfaces;

public interface ILayerRenderer
{
    void Draw(object canvas, EvaluationContext context);
}
