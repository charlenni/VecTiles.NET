using VecTiles.Common.Enums;
using VecTiles.Common.Primitives;

namespace VecTiles.Common.Interfaces;

/// <summary>
/// Interface for a style file independent class, that could produce one or more SKPaint
/// </summary>
public interface IPaint
{
    /// <summary>
    /// Painting style defining whether the geometry is filled, stroked or both.
    /// </summary>
    PaintStyle Style { get; }

    /// <summary>
    /// Fill color.
    /// </summary>
    Color Color { get; }

    /// <summary>
    /// Outline color (only used by fill layers).
    /// </summary>
    Color OutlineColor { get; }

    /// <summary>
    /// Opacity of the painting (1.0 is fully opaque).
    /// </summary>
    float Opacity { get; }

    /// <summary>
    /// Gets a value indicating whether antialiasing is used when rendering the geometry.
    /// </summary>
    bool IsAntialias { get; }

    /// <summary>
    /// Width of the stroke in style px.
    /// </summary>
    float StrokeWidth { get; }

    /// <summary>
    /// Cap of a stroked line.
    /// </summary>
    StrokeCap StrokeCap { get; }

    /// <summary>
    /// Join of a stroked line.
    /// </summary>
    StrokeJoin StrokeJoin { get; }

    /// <summary>
    /// Maximum length of the miter join of a stroked line.
    /// </summary>
    float StrokeMiter { get; }

    /// <summary>
    /// Maximum miter ratio up to which ROUND joins of a stroked line are
    /// converted to miter joins (shallow angles). Positive infinity
    /// disables the conversion.
    /// </summary>
    float StrokeRoundLimit { get; }

    /// <summary>
    /// Pattern sprite used for filling.
    /// </summary>
    ISprite? Pattern { get; }

    /// <summary>
    /// Dash pattern as an array of dash and gap lengths in tile-space px, or
    /// <c>null</c> for a solid line. The converters scale their style units
    /// themselves.
    /// </summary>
    float[]? DashArray { get; }

    /// <summary>
    /// Radius of a circle in style px (only used by circle layers)
    /// </summary>
    float Radius { get; }

    /// <summary>
    /// Blur amount of a circle (only used by circle layers)
    /// </summary>
    float Blur { get; }

    /// <summary>
    /// Gets a value indicating whether any paint property is data-driven, i.e.
    /// is evaluated from a variable function instead of a fixed value.
    /// </summary>
    bool IsDataDriven { get; }

    /// <summary>
    /// Updates all properties of the paint using the given evaluation context.
    /// </summary>
    /// <param name="context">The evaluation context with zoom information and feature attributes.</param>
    void Update(EvaluationContext context);

    /// <summary>
    /// Gets a paint whose variable properties are evaluated for the given
    /// feature context and then fixed to these values (the paint of a group
    /// of features whose data-driven properties evaluate equally). The
    /// snapshot paint keeps these evaluated values regardless of the context
    /// of later <see cref="Update"/> calls at draw time, so the geometry is
    /// drawn with exactly the values the features were grouped by.
    /// </summary>
    /// <param name="featureContext">The evaluation context with the feature and the tile information (zoom, scale).</param>
    /// <returns>The paint evaluated and fixed to the values of the given context.</returns>
    IPaint SnapshotFor(EvaluationContext featureContext);

    /// <summary>
    /// Gets the maximum stroke width of the paint, i.e. the (largest) stroke width evaluated at the current zoom.
    /// </summary>
    /// <param name="context">The evaluation context with zoom information and feature attributes.</param>
    /// <returns>The maximum stroke width in style px.</returns>
    float GetMaxStrokeWidth(EvaluationContext context);
}
