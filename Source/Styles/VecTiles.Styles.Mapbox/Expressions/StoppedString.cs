using VecTiles.Common.Primitives;

namespace VecTiles.Styles.Mapbox.Expressions;

/// <summary>
/// Class holding StoppedString data
/// </summary>
public class StoppedString : IStopped<string>
{
    bool _hasDefault = false;
    string _default = string.Empty;

    /// <summary>
    /// Default value to use, if nothing else is set
    /// </summary>
    public string Default
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
    string _singleVal = string.Empty;

    /// <summary>
    /// SingleVal to use, if there are no stops
    /// </summary>
    public string SingleVal
    {
        get => _singleVal;
        set
        {
            _hasSingleVal = true;
            _singleVal = value;
        }
    }

    bool _hasStops = false;
    IList<KeyValuePair<float, string>> _stops = [];

    public float Base { get; set; } = 1f;

    public IList<KeyValuePair<float, string>> Stops
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
    /// <returns>Value for this stopp respecting resolution factor</returns>
    public string Evaluate(EvaluationContext ctx)
    {
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

        return string.Empty;
    }

    public string PossibleOutputs()
    {
        throw new System.NotImplementedException();
    }

    public override string ToString()
    {
        if (_hasStops && _stops.Count > 0)
        {
            return "StoppedString [Stops= " + string.Join(",", _stops.Select(kvp => $"[{kvp.Key}, {kvp.Value}]"));
        }

        if (_hasSingleVal)
        {
            return $"StoppedString [SingleVal='{SingleVal}']";
        }

        return $"StoppedString [Default='{Default}']";
    }
}
