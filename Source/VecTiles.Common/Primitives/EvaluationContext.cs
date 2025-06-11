using NetTopologySuite.Features;

namespace VecTiles.Common.Primitives;

/// <summary>
/// Represents the context in which a style should be evaluated, including zoom level, 
/// scale, rotation, feature attributes, and the feature itself. Implements equality 
/// comparison based on all context properties.
/// </summary>
public class EvaluationContext : IEquatable<EvaluationContext>
{
    public static readonly EvaluationContext Empty = new EvaluationContext(0);

    public EvaluationContext(float? zoom, float scale = 1, float rotation = 0, AttributesTable? attributes = null, IFeature? feature = null)
    {
        Zoom = zoom;
        Scale = scale;
        Rotation = rotation;
        Attributes = attributes;
        Feature = feature;
    }

    /// <summary>
    /// Gets or sets the zoom level for the evaluation context.
    /// </summary>
    public float? Zoom { get; set; }

    /// <summary>
    /// Gets or sets the scale factor for the evaluation context.
    /// </summary>
    public float Scale { get; set; }

    /// <summary>
    /// Gets or sets the rotation (in degrees) for the evaluation context.
    /// </summary>
    public float Rotation { get; set; }

    /// <summary>
    /// Gets or sets the attributes table for the evaluation context.
    /// </summary>
    public AttributesTable? Attributes { get; set; }

    /// <summary>
    /// Gets or sets the feature for the evaluation context if it should be used.
    /// </summary>
    public IFeature? Feature { get; set; }

    /// <summary>
    /// Overrides the base Equals method to provide value-based equality for EvaluationContext instances.
    /// </summary>
    /// <param name="obj">The object to compare with the current context.</param>
    /// <returns></returns>
    public override bool Equals(object? obj) => Equals(obj as EvaluationContext);

    /// <summary>
    /// Determines whether the specified EvaluationContext is equal to the current EvaluationContext.
    /// Compares all context properties: Zoom, Scale, Rotation, Attributes, and Feature.
    /// </summary>
    /// <param name="other">The EvaluationContext to compare with the current context.</param>
    /// <returns>true if the specified context is equal to the current context; otherwise, false.</returns>
    public bool Equals(EvaluationContext? other)
    {
        if (ReferenceEquals(this, other))
            return true;
        if (other is null)
            return false;

        return Nullable.Equals(Zoom, other.Zoom)
            && Scale.Equals(other.Scale)
            && Rotation.Equals(other.Rotation)
            && Equals(Attributes, other.Attributes)
            && Equals(Feature, other.Feature);
    }

    /// <summary>
    /// Returns a hash code for the EvaluationContext, combining all context properties.
    /// </summary>
    public override int GetHashCode()
    {
        unchecked
        {
            int hashCode = Zoom.HasValue ? Zoom.Value.GetHashCode() : 0;
            hashCode = (hashCode * 587) ^ Scale.GetHashCode();
            hashCode = (hashCode * 587) ^ Rotation.GetHashCode();
            hashCode = (hashCode * 587) ^ (Attributes?.GetHashCode() ?? 0);
            hashCode = (hashCode * 587) ^ (Feature?.GetHashCode() ?? 0);
            return hashCode;
        }
    }
}
