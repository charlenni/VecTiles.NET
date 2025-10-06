using NetTopologySuite.Features;
using VecTiles.Common.Primitives;

namespace VecTiles.Common.Interfaces;

public interface ISymbolFactory
{
    ISymbol? CreateSymbol(Tile tile, ILayerStyle style, EvaluationContext context, IFeature feature);
}
