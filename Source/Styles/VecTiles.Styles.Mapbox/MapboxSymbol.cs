using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Quadtree;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;

namespace VecTiles.Styles.Mapbox;

public abstract class MapboxSymbol : ISymbol
{
    public MapboxSymbol(Tile tile)
    {
        Tile = tile;
    }

    /// <summary>
    /// Tile to which this symbol belongs
    /// </summary>
    public Tile Tile { get; }

    /// <summary>
    /// Name of feature this symbol belongs to
    /// </summary>
    public string Name { get; internal set; }

    /// <summary>
    /// Sort order to use for this symbol
    /// </summary>
    public double SortOrder { get; internal set; }

    /// <summary>
    /// Class of this symbol as string
    /// </summary>
    public string Class { get; internal set; } = string.Empty;

    /// <summary>
    /// Subclass of this symbol as string
    /// </summary>
    public string Subclass { get; internal set; } = string.Empty;

    /// <summary>
    /// Rank of this symbol as integer
    /// </summary>
    public int Rank { get; internal set; } = 0;

    /// <summary>
    /// Could other symbols occupie the same space 
    /// </summary>
    public bool AllowOthers { get; internal set; }

    // TODO
    // Remove, for test only
    public Envelope Envelope { get; protected set; }

    public abstract bool CheckForSpace(SKCanvas canvas, EvaluationContext context, Quadtree<ISymbol> tree, Func<double, double, (double, double)> worldToScreenConverter, bool showValidBorders = false, bool showUnvalidBorders = false);

    public abstract void Draw(SKCanvas canvas, EvaluationContext context, ref Quadtree<ISymbol> tree, Func<double, double, (double, double)> worldToScreenConverter);

    internal static (float rotationDeg, float scaleX, float scaleY, SKPoint translation) AnalyzeCanvasTransform(SKCanvas canvas)
    {
        var m = canvas.TotalMatrix;

        // Rotation berechnen (in Grad)
        float rotationRad = (float)Math.Atan2(m.SkewY, m.ScaleY);
        float rotationDeg = (float)(rotationRad * (180f / Math.PI));

        // Skalierung berechnen (Betrag der Vektoren)
        float scaleX = (float)Math.Sqrt(m.ScaleX * m.ScaleX + m.SkewX * m.SkewX);
        float scaleY = (float)Math.Sqrt(m.SkewY * m.SkewY + m.ScaleY * m.ScaleY);

        // Translation (Position)
        var translation = new SKPoint(m.TransX, m.TransY);

        return (rotationDeg, scaleX, scaleY, translation);
    }
}
