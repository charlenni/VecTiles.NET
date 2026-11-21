namespace VecTiles.Renderers.Skia;

/// <summary>
/// Placement decision for one anchor position of a LineSymbol, as determined
/// by <see cref="LineSymbolRenderer.Place"/>. Drawing consumes these recorded
/// decisions without running the collision checks again.
/// </summary>
public sealed record LineSymbolAnchorPlacement(float Position, bool Icon, bool Text, PathTextPlacement Placement);

/// <summary>
/// Placement decisions for all contours of a LineSymbol, in the order of the
/// contours of the geometry. A run can be empty when no anchor of the symbol
/// could be placed.
/// </summary>
public sealed class LineSymbolPlacementRun
{
    /// <summary>
    /// Run without any placed anchors, used when nothing of the symbol fits
    /// onto the line.
    /// </summary>
    public static readonly LineSymbolPlacementRun Empty = new(Array.Empty<IReadOnlyList<LineSymbolAnchorPlacement>>());

    /// <summary>
    /// Create a placement run for already placed anchor decisions.
    /// </summary>
    /// <param name="contours">Placed anchors per geometry contour, in contour order.</param>
    public LineSymbolPlacementRun(IReadOnlyList<IReadOnlyList<LineSymbolAnchorPlacement>> contours)
    {
        Contours = contours;
    }

    /// <summary>
    /// Gets the placed anchors per geometry contour, in contour order.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<LineSymbolAnchorPlacement>> Contours { get; }

    /// <summary>
    /// True when at least one anchor of the symbol was placed.
    /// </summary>
    public bool HasPlacements => Contours.Any(c => c.Count > 0);
}
