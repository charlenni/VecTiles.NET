// Copyright NetTopologySuite Contributors

using NetTopologySuite.Features;

namespace VecTiles.Common.Primitives;

/// <summary>
/// Abstract representation of a layer.
/// </summary>
public class Layer
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Layer"/> class with capacity for the given number of features.
    /// </summary>
    /// <param name="numOfFeatures">The expected number of features. Defaults to 0.</param>
    public Layer(int numOfFeatures = 0)
    {
        Features = new List<IFeature>(numOfFeatures);
    }
    
    /// <summary>
    /// Gets or sets the name of the layer.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets the is empty flag.
    /// </summary>
    public virtual bool IsEmpty => Features.Count == 0;

    /// <summary>
    /// Gets the features.
    /// </summary>
    public IList<IFeature> Features { get; }
}
