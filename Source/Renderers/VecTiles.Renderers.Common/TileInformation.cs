using VecTiles.Common.Primitives;

namespace VecTiles.Renderers.Common;

public record TileInformation
{
    public bool Border { get; init; } = false;

    public bool Text { get; init; } = false;

    public float BorderSize { get; init; } = 2;

    public float TextSize { get; init; } = 20;

    public Color Color { get; init; } = new Color(255, 0, 0, 255);
}
