using VecTiles.Common.Extensions;
using VecTiles.Common.Primitives;

namespace VecTiles.Styles.Mapbox.Expressions;

internal class MGLColorType : MGLType
{
    public MGLColorType(string v)
    {
        Value = v.FromString();
    }

    public MGLColorType(Color v)
    {
        Value = v;
    }

    public Color Value { get; }

    public override string ToString()
    {
        return "color";
    }
}
