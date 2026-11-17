using NetTopologySuite.Features;
using VecTiles.Common.Interfaces;

namespace VecTiles.Styles.OpenMapTiles.Filter;

/// <summary>
/// Base class for compound filters that combine multiple filters using logical operations.
/// </summary>
public abstract class CompoundFilter : Filter
{
    /// <summary>
    /// The list of filters to be combined.
    /// </summary>
    public List<IFilter> Filters { get; }

    /// <summary>
    /// Initializes a new compound filter with an empty filter list.
    /// </summary>
    public CompoundFilter()
    {
        Filters = new List<IFilter>();
    }

    /// <summary>
    /// Initializes a new compound filter with the specified filters.
    /// </summary>
    /// <param name="filters">The filters to combine. Null values are ignored.</param>
    public CompoundFilter(List<IFilter> filters)
    {
        Filters = new List<IFilter>(filters?.Count ?? 0);

        if (filters != null)
        {
            foreach (var filter in filters)
            {
                if (filter != null)
                    Filters.Add(filter);
            }
        }
    }

    /// <summary>
    /// Evaluates whether the feature passes the combined filter logic.
    /// </summary>
    /// <param name="feature">The feature to evaluate.</param>
    /// <returns>True if the feature passes the combined filter; otherwise, false.</returns>
    public abstract override bool Evaluate(IFeature feature);
}
