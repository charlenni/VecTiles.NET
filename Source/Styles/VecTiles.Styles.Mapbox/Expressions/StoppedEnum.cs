using VecTiles.Common.Primitives;

namespace VecTiles.Styles.Mapbox.Expressions;

/// <summary>
/// Class holding StoppedEnum data
/// </summary>
public class StoppedEnum<T> : IStopped<T> where T : struct, Enum
{
    bool _hasDefault = false;
    T _default;

    /// <summary>
    /// Default value to use, if nothing else is set
    /// </summary>
    public T Default
    {
        get => _default;
        set
        {
            _hasDefault = true;
            _default = value;
        }
    }

    /// <summary>
    /// Is this a stopped value that only contains the default
    /// </summary>
    public bool HasOnlyDefault => _hasDefault & !(_hasSingleVal | _hasStops);

    /// <summary>
    /// True, if value doesn't change with context
    /// </summary>
    public bool IsConstant => !_hasStops;

    bool _hasSingleVal = false;
    T _singleVal = default(T);

    /// <summary>
    /// SingleVal to use, if there are no stops
    /// </summary>
    public T SingleVal
    {
        get => _singleVal;
        set
        {
            _hasSingleVal = true;
            _singleVal = value;
        }
    }

    bool _hasStops = false;
    IList<KeyValuePair<float, T>> _stops = [];

    public float Base { get; set; } = 1f;

    public IList<KeyValuePair<float, T>> Stops
    {
        get => _stops;
        set
        {
            _hasStops = true;
            _stops = value;
        }
    }

    /// <summary>
    /// Calculate the correct string for a stopped function
    /// No StoppsType needed, because strings couldn't interpolated :)
    /// </summary>
    /// <param name="contextZoom">Zoom factor for calculation </param>
    /// <returns>Value for this stopp respecting resolution factor and type</returns>
    public T Evaluate(EvaluationContext ctx)
    {
        // Are there no stopps in array
        if (Stops.Count == 0)
            return SingleVal;

        if (_hasStops)
        {
            float zoom = ctx.Zoom ?? 0f;

            var lastZoom = Stops[0].Key;
            var lastValue = Stops[0].Value;

            if (lastZoom > zoom)
                return lastValue;

            for (int i = 1; i < Stops.Count; i++)
            {
                var nextZoom = Stops[i].Key;
                var nextValue = Stops[i].Value;

                if (zoom == nextZoom)
                    return nextValue;

                if (lastZoom <= zoom && zoom < nextZoom)
                {
                    return lastValue;
                }

                lastZoom = nextZoom;
                lastValue = nextValue;
            }

            return lastValue;
        }

        if (_hasSingleVal)
        {
            return SingleVal;
        }

        if (_hasDefault)
        {
            return Default;
        }

        return default(T);
    }

    public T PossibleOutputs()
    {
        throw new System.NotImplementedException();
    }

    public override string ToString()
    {
        if (_hasStops && _stops.Count > 0)
        {
            return "StoppedEnum [Stops= " + string.Join(",", _stops.Select(kvp => $"[{kvp.Key}, {kvp.Value}]"));
        }

        if (_hasSingleVal)
        {
            return $"StoppedEnum [SingleVal={SingleVal}]";
        }

        return $"StoppedEnum [Default={Default}]";
    }
}
