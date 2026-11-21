namespace VecTiles.Common.Enums;

/// <summary>
/// Specifies how the ends of a stroked line are drawn.
/// </summary>
public enum StrokeCap
{
    /// <summary>
    /// Ends the stroke exactly at the endpoint, with no cap.
    /// </summary>
    Butt,
    /// <summary>
    /// Extends the stroke with a semicircular cap around the endpoint.
    /// </summary>
    Round,
    /// <summary>
    /// Extends the stroke with a square cap of half the stroke width beyond the endpoint.
    /// </summary>
    Square
}
