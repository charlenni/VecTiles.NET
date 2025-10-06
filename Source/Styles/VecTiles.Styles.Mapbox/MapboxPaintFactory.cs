using VecTiles.Common.Enums;
using VecTiles.Common.Interfaces;

namespace VecTiles.Styles.Mapbox;

public class MapboxPaintFactory : IPaintFactory
{
    Func<string, MapboxSprite> _spriteFactory;

    public MapboxPaintFactory(MapboxSpriteFile? spriteFile) 
    {
        if (spriteFile == null)
            throw new ArgumentNullException(nameof(spriteFile));

        _spriteFactory = (name) =>
        {
            var sprite = spriteFile.Sprites[name];

            return sprite;
        };
    }

    public IPaint? CreatePaint(ILayerStyle style)
    {
        return style.StyleType switch
        {
            LayerStyleType.Background => MapboxBackgroundPaint.CreatePaint(style, _spriteFactory),
            LayerStyleType.Raster => MapboxRasterPaint.CreatePaint(style, _spriteFactory),
            LayerStyleType.Fill => MapboxFillPaint.CreatePaint(style, _spriteFactory),
            LayerStyleType.Line => MapboxLinePaint.CreatePaint(style, _spriteFactory),
            LayerStyleType.Symbol => null,
            LayerStyleType.FillExtrusion => null,
            _ => throw new NotImplementedException($"Layer style with type '{style.StyleType}' is unknown")
        };
    }
}
