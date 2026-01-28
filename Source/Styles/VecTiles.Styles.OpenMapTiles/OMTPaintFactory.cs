using VecTiles.Common.Enums;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;

namespace VecTiles.Styles.OpenMapTiles;

public class OMTPaintFactory : IPaintFactory
{
    Func<string, Sprite> _spriteFactory;

    public OMTPaintFactory(SpriteDicionary? spriteDictionary) 
    {
        if (spriteDictionary == null)
            throw new ArgumentNullException(nameof(spriteDictionary));

        _spriteFactory = (name) =>
        {
            var sprite = spriteDictionary.Sprites[name];

            return sprite;
        };
    }

    public IPaint? CreatePaint(ILayerStyle style)
    {
        return style.StyleType switch
        {
            LayerStyleType.Background => OMTBackgroundPaint.CreatePaint(style, _spriteFactory),
            LayerStyleType.Raster => OMTRasterPaint.CreatePaint(style, _spriteFactory),
            LayerStyleType.Fill => OMTFillPaint.CreatePaint(style, _spriteFactory),
            LayerStyleType.Line => OMTLinePaint.CreatePaint(style, _spriteFactory),
            LayerStyleType.Symbol => null,
            LayerStyleType.FillExtrusion => null,
            _ => throw new NotImplementedException($"Layer style with type '{style.StyleType}' is unknown")
        };
    }
}
