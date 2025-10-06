namespace VecTiles.Common.Interfaces;

/// <summary>
/// Interface to create a style file independent class for each style
/// </summary>
public interface IPaintFactory
{
    IPaint? CreatePaint(ILayerStyle style);
}
