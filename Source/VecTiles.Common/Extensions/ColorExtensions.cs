using VecTiles.Common.Primitives;

namespace VecTiles.Common.Extensions;

public static class ColorExtensions
{
    public static Color WithAlpha(this Color color, byte alpha)
    {
        return color with { A = alpha };
    }
}
