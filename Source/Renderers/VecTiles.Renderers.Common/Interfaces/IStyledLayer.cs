using VecTiles.Common.Primitives;

namespace VecTiles.Renderers.Common.Interfaces;

public interface IStyledLayer
{
    void Draw(object canvas, EvaluationContext context);
}
