namespace VecTiles.Common.Interfaces;

public interface ISprite : IBitmapRegion
{
    IBitmap Atlas { get; }
    
    float PixelRatio { get; }
}
