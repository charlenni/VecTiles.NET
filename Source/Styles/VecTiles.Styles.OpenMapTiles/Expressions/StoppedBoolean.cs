using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;

namespace VecTiles.Styles.OpenMapTiles.Expressions;

/// <summary>
/// Class holding StoppedBoolean data
/// </summary>
public class StoppedBoolean : IStopped<bool>
{
    bool _hasDefault = false;
    bool _default;

    /// <summary>
    /// Default value to use, if nothing else is set
    /// </summary>
    public bool Default
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
    public bool HasOnlyDefault => _hasDefault & !(_hasSingleVal | _hasStops) && Expression == null;

    /// <summary>
    /// True, if value doesn't change with context
    /// </summary>
    public bool IsConstant => !_hasStops && Expression == null;

    /// <summary>
    /// Expression to evaluate, e.g. ["get", "property"]. Data-driven form per
    /// Mapbox Style Spec. If set, it takes precedence over stops, single value
    /// and default.
    /// </summary>
    public IExpression? Expression { get; set; }

    bool _hasSingleVal = false;
    bool _singleVal;

    /// <summary>
    /// SingleVal to use, if there are no stops
    /// </summary>
    public bool SingleVal
    {
        get => _singleVal;
        set
        {
            _hasSingleVal = true;
            _singleVal = value;
        }
    }

    bool _hasStops = false;
    IList<KeyValuePair<float, bool>> _stops = [];

    /// <summary>The interpolation base of the stop curve. Booleans are not interpolated, so this value has no effect on the evaluation.</summary>
    public float Base { get; set; } = 1f;

    /// <summary>The zoom/value stops of the stopped function, in ascending zoom order.</summary>
    public IList<KeyValuePair<float, bool>> Stops
    {
        get => _stops;
        set
        {
            _hasStops = true;
            _stops = value;
        }
    }

    /// <summary>
    /// Calculate the correct boolean for a stopped function
    /// No StopsType needed, because booleans couldn't interpolated :)
    /// </summary>
    /// <param name="contextZoom">Zoom factor for calculation </param>
    /// <returns>Value for this stopp respecting resolution factor and type</returns>
    public bool Evaluate(EvaluationContext ctx)
    {
        if (Expression != null)
        {
            return StoppedExpression.ToBoolean(Expression.Evaluate(ctx));
        }

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

        return false;
    }

    /// <inheritdoc />
    public bool PossibleOutputs()
    {
        throw new System.NotImplementedException();
    }

    /// <inheritdoc />
    public override string ToString()
    {
        if (Expression != null)
        {
            return "StoppedBoolean [Expression]";
        }

        if (_hasStops && _stops.Count > 0)
        {
            return "StoppedBoolean [Stops= " + string.Join(",", _stops.Select(kvp => $"[{kvp.Key}, {kvp.Value}]"));
        }

        if (_hasSingleVal)
        {
            return $"StoppedBoolean [SingleVal={SingleVal}]";
        }

        return $"StoppedBoolean [Default={Default}]";
    }
}
